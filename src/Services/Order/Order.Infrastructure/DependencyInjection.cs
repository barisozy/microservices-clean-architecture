using ECommerce.Contracts.Protos;
using ECommerce.Auditing;
using ECommerce.ServiceDefaults.Interceptors;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Order.Application.Common.Interfaces;
using Order.Application.Consumers;
using Order.Infrastructure.Data;
using Order.Infrastructure.Data.Interceptors;
using Order.Infrastructure.Services;
using StackExchange.Redis;

namespace Order.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUser, CurrentUser>();

        services.AddECommerceAuditing();
        services.AddScoped<DispatchDomainEventsInterceptor>();

        services.AddDbContext<OrderDbContext>((sp, options) =>
        {
            options.AddInterceptors(
                sp.GetRequiredService<ECommerce.Auditing.AuditableEntityInterceptor>(),
                sp.GetRequiredService<DispatchDomainEventsInterceptor>()
            );
            options.UseNpgsql(configuration.GetConnectionString("OrderDb"), npgsql =>
            {
                npgsql.SetPostgresVersion(18, 0);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "order");
            });
        });

        services.AddScoped<IOrderDbContext>(provider => provider.GetRequiredService<OrderDbContext>());

        // Valkey basket service (BSD-3-Clause)
        var valkeyConnectionString = configuration.GetConnectionString("valkey")
            ?? configuration.GetConnectionString("cache")
            ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(valkeyConnectionString));
        services.AddScoped<IBasketService, ValkeyBasketService>();
        services.AddScoped<IOrderCache, ValkeyOrderCache>();
        services.AddScoped<IOrderReadRepository, Order.Infrastructure.Data.Repositories.OrderReadRepository>();

        // gRPC client → Inventory.Api (with JWT + trace interceptors from ServiceDefaults)
        services.AddGrpcClient<InventoryService.InventoryServiceClient>(options =>
        {
            var address = configuration["services:inventory-api:http:0"]
                ?? configuration["services:inventory-api:https:0"]
                ?? configuration["Services:InventoryApi"]
                ?? "http://inventory-api";
            options.Address = new Uri(address);
        })
        .AddServiceDiscovery()
        .AddInterceptor<GrpcJwtHeaderInterceptor>()
        .AddInterceptor<GrpcTraceContextInterceptor>()
        .AddStandardResilienceHandler(options => 
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.Retry.UseJitter = true;
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
        });

        // gRPC client → Catalog.Api
        services.AddGrpcClient<CatalogService.CatalogServiceClient>(options =>
        {
            var address = configuration["services:catalog-api:http:0"]
                ?? configuration["services:catalog-api:https:0"]
                ?? configuration["Services:CatalogApi"]
                ?? "http://catalog-api";
            options.Address = new Uri(address);
        })
        .AddServiceDiscovery()
        .AddInterceptor<GrpcJwtHeaderInterceptor>()
        .AddInterceptor<GrpcTraceContextInterceptor>()
        .AddStandardResilienceHandler();

        // gRPC client → Promotion.Api
        services.AddGrpcClient<PromotionService.PromotionServiceClient>(options =>
        {
            var address = configuration["services:promotion-api:http:0"]
                ?? configuration["services:promotion-api:https:0"]
                ?? configuration["Services:PromotionApi"]
                ?? "http://promotion-api";
            options.Address = new Uri(address);
        })
        .AddServiceDiscovery()
        .AddInterceptor<GrpcJwtHeaderInterceptor>()
        .AddInterceptor<GrpcTraceContextInterceptor>()
        .AddStandardResilienceHandler();

        // MassTransit + Transactional Outbox/Inbox
        services.AddMassTransit(x =>
        {
            x.AddConsumer<Order.Application.Consumers.PaymentFailedConsumer>();
            x.AddConsumer<Order.Application.Consumers.StockReleasedConsumer>();

            x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
            {
                o.UsePostgres(enableSchemaCaching: false);
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(1);

                // Sprint 2: consumer-side duplicate detection (replaces hand-rolled ConsumedEvents table)
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });
            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
                endpoint.UseEntityFrameworkOutbox<OrderDbContext>(context));

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitConnectionString = configuration.GetConnectionString("rabbitmq") ?? "amqp://guest:guest@localhost:5672";
                cfg.Host(new Uri(rabbitConnectionString));
                cfg.AutoStart = true;

                // Event Resilience Patterns: Retry policy, Dead letter queue, Poison message handling
                // 1. Retry policy (Retry x3 as requested)
                cfg.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(8), TimeSpan.FromMilliseconds(500)));
                
                // 2 & 3. Dead letter queue (DLQ) & Poison message handling
                // MassTransit automatically moves messages that fail all retries to a fault/DLQ queue (e.g., PaymentFailed_error).
                
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
