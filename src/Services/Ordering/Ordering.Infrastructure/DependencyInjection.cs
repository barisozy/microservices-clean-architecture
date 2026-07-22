using ECommerce.Contracts.Protos;
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

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<OrderingDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(configuration.GetConnectionString("OrderingDb"), npgsql =>
            {
                npgsql.SetPostgresVersion(18, 0);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ordering");
            });
        });

        services.AddScoped<IOrderingDbContext>(provider => provider.GetRequiredService<OrderingDbContext>());

        // Redis basket service (Valkey, BSD-3-Clause)
        var redisConnectionString = configuration.GetConnectionString("redis")
            ?? configuration.GetConnectionString("cache")
            ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddScoped<IBasketService, RedisBasketService>();

        // gRPC client → Inventory.Api (with JWT + trace interceptors from ServiceDefaults)
        services.AddGrpcClient<InventoryService.InventoryServiceClient>(options =>
        {
            options.Address = new Uri(configuration["Services:InventoryApi"] ?? "http://inventory-api");
        })
        .AddInterceptor<GrpcJwtHeaderInterceptor>()
        .AddInterceptor<GrpcTraceContextInterceptor>()
        .AddStandardResilienceHandler();   // Retry + circuit-breaker via Microsoft.Extensions.Http.Resilience

        // MassTransit + Transactional Outbox/Inbox
        services.AddMassTransit(x =>
        {
            x.AddConsumer<PaymentFailedConsumer>();

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

                cfg.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(8), TimeSpan.FromMilliseconds(200)));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}

