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
using Order.Application.Checkout;
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

        

        // Valkey basket service (BSD-3-Clause)
        var valkeyConnectionString = configuration.GetConnectionString("valkey")
            ?? configuration.GetConnectionString("cache")
            ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(valkeyConnectionString));
        services.AddScoped<IBasketService, ValkeyBasketService>();
        services.AddScoped<IOrderWriteRepository, Order.Infrastructure.Data.Repositories.OrderWriteRepository>();
        services.AddScoped<IOrderCache, ValkeyOrderCache>();
        services.AddScoped<IOrderReadRepository, Order.Infrastructure.Data.Repositories.OrderReadRepository>();
        services.AddSingleton(TimeProvider.System);

        // gRPC client → Inventory.Api (with JWT + trace interceptors from ServiceDefaults)
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
            x.AddDelayedMessageScheduler();
            x.AddSagaStateMachine<CheckoutStateMachine, CheckoutState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ConcurrencyMode = ConcurrencyMode.Optimistic;
                    r.ExistingDbContext<OrderDbContext>();
                    r.UsePostgres();
                });
            x.AddConsumer<Order.Application.Consumers.PaymentFailedConsumer>();
            x.AddConsumer<Order.Application.Consumers.StockReleasedConsumer>();
            x.AddConsumer<Order.Application.Consumers.PaymentCompletedConsumer>();
            x.AddConsumer<Order.Application.Consumers.OrderShippedConsumer>();
            x.AddConsumer<Order.Application.Consumers.OrderCancelledConsumer>();
            x.AddConsumer<Order.Application.Consumers.OrderInventoryConfirmedConsumer>();

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
                cfg.UseDelayedMessageScheduler();
                cfg.AutoStart = true;
                // Event Resilience Patterns: Retry policy, Dead letter queue, Poison message handling
                // 1. Retry policy (Retry x3 as requested)
                // PaymentCompleted and OrderShipped are delivered to independent
                // endpoints. OrderShipped may therefore arrive before the Paid
                // transaction commits. Keep the aggregate invariant strict and
                // allow the broker retry window to absorb that valid reordering.
                if (string.Equals(configuration["DOTNET_ENVIRONMENT"], "IntegrationTesting", StringComparison.OrdinalIgnoreCase))
                    cfg.UseMessageRetry(r => r.Immediate(2));
                else
                    cfg.UseMessageRetry(r => r.Exponential(10, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(8), TimeSpan.FromMilliseconds(500)));
                
                // 2 & 3. Dead letter queue (DLQ) & Poison message handling
                // MassTransit automatically moves messages that fail all retries to a fault/DLQ queue (e.g., PaymentFailed_error).
                
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}

