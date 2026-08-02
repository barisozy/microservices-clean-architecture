using Customer.Infrastructure.Consumers;
using Customer.Infrastructure.Data;
using Customer.Application.Common.Interfaces;
using Customer.Infrastructure.Services;
using ECommerce.Contracts.Protos;
using ECommerce.ServiceDefaults.Interceptors;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Customer.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("customer_db") 
            ?? configuration.GetConnectionString("CustomerDb")
            ?? "Host=localhost;Database=customer_db;Username=postgres;Password=postgres";

        services.AddDbContext<CustomerDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.SetPostgresVersion(18, 0);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "customer");
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
            x.AddConsumer<UserRegisteredConsumer>();

            x.AddEntityFrameworkOutbox<CustomerDbContext>(o =>
            {
                o.UsePostgres(enableSchemaCaching: false);
                o.UseBusOutbox();
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });
            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
                endpoint.UseEntityFrameworkOutbox<CustomerDbContext>(context));

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
