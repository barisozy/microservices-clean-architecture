using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Promotion.Infrastructure.Data;
using Promotion.Application.Common.Interfaces;
using Promotion.Infrastructure.Services;
using ECommerce.Contracts.Protos;
using ECommerce.ServiceDefaults.Interceptors;

namespace Promotion.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("promotion_db") 
            ?? configuration.GetConnectionString("PromotionDb")
            ?? "Host=localhost;Database=promotion_db;Username=postgres;Password=postgres";

        services.AddDbContext<PromotionDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.SetPostgresVersion(18, 0);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "promotion");
            });
        });

        services.AddGrpcClient<IamService.IamServiceClient>(options =>
        {
            var address = configuration["services:iam-api:http:0"]
                ?? configuration["services:iam-api:https:0"]
                ?? configuration["Services:IamApi"]
                ?? "http://iam-api";
            options.Address = new Uri(address);
        })
        .AddServiceDiscovery()
        .AddInterceptor<GrpcJwtHeaderInterceptor>()
        .AddInterceptor<GrpcTraceContextInterceptor>()
        .AddStandardResilienceHandler();
        services.AddScoped<IIamPermissionChecker, IamPermissionChecker>();

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<PromotionDbContext>(o =>
            {
                o.UsePostgres(enableSchemaCaching: false);
                o.UseBusOutbox();
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitHost = configuration.GetConnectionString("rabbitmq")
                    ?? configuration["EventBus:HostAddress"]
                    ?? "amqp://guest:guest@localhost:5672";
                cfg.Host(new Uri(rabbitHost));
                cfg.AutoStart = true;
                cfg.PrefetchCount = 16;
                cfg.UseMessageRetry(retry => retry.Exponential(
                    5,
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromSeconds(8),
                    TimeSpan.FromMilliseconds(500)));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
