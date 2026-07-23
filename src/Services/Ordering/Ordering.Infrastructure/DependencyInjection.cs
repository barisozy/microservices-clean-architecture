using ECommerce.Contracts.Protos;
using ECommerce.Auditing;
using ECommerce.ServiceDefaults.Interceptors;
using Grpc.Net.Client;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Common.Interfaces;
using Ordering.Application.Consumers;
using Ordering.Infrastructure.Data;
using Ordering.Infrastructure.Data.Interceptors;
using Ordering.Infrastructure.Services;
using StackExchange.Redis;

namespace Ordering.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUser, CurrentUser>();
        services.AddECommerceAuditing();

        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<DispatchDomainEventsInterceptor>();

        services.AddDbContext<OrderingDbContext>((sp, options) =>
        {
            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntityInterceptor>(),
                sp.GetRequiredService<DispatchDomainEventsInterceptor>()
            );
            options.UseNpgsql(configuration.GetConnectionString("OrderingDb"), npgsql =>
            {
                npgsql.SetPostgresVersion(18, 0);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ordering");
            });
        });

        services.AddScoped<IOrderingDbContext>(provider => provider.GetRequiredService<OrderingDbContext>());

        // Valkey basket service (BSD-3-Clause)
        var valkeyConnectionString = configuration.GetConnectionString("valkey")
            ?? configuration.GetConnectionString("cache")
            ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(valkeyConnectionString));
        services.AddScoped<IBasketService, ValkeyBasketService>();
        services.AddScoped<IOrderReadRepository, Ordering.Infrastructure.Data.Repositories.OrderReadRepository>();

        // gRPC client → Inventory.Api (with JWT + trace interceptors from ServiceDefaults)
        services.AddGrpcClient<InventoryService.InventoryServiceClient>(options =>
        {
            options.Address = new Uri(configuration["Services:InventoryApi"] ?? "http://inventory-api");
        })
        .AddInterceptor<GrpcJwtHeaderInterceptor>()
        .AddInterceptor<GrpcTraceContextInterceptor>()
        .AddStandardResilienceHandler(options => 
        {
            // gRPC Resilience Patterns
            
            // 1. Retry
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            
            // 2. Circuit Breaker
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
            
            // 3. Deadline (Attempt Timeout)
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
            
            // 4. Global Timeout
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
        });

        // MassTransit + Transactional Outbox/Inbox
        services.AddMassTransit(x =>
        {
            x.AddConsumer<StockReleasedConsumer>();

            x.AddEntityFrameworkOutbox<OrderingDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();

                // Sprint 2: consumer-side duplicate detection (replaces hand-rolled ConsumedEvents table)
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitConnectionString = configuration.GetConnectionString("rabbitmq") ?? "amqp://guest:guest@localhost:5672";
                cfg.Host(new Uri(rabbitConnectionString));

                // Event Resilience Patterns: Retry policy, Dead letter queue, Poison message handling
                // 1. Retry policy (Retry x3 as requested)
                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                
                // 2 & 3. Dead letter queue (DLQ) & Poison message handling
                // MassTransit automatically moves messages that fail all retries to a fault/DLQ queue (e.g., PaymentFailed_error).
                
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}

