using Audit.Application.Common.Interfaces;
using Audit.Application.Consumers;
using Audit.Infrastructure.Data;
using Audit.Infrastructure.Services;
using ECommerce.Contracts.Protos;
using ECommerce.ServiceDefaults.Interceptors;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Audit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("AuditDb")
            ?? configuration.GetConnectionString("audit_db")
            ?? "Host=localhost;Database=audit_db;Username=postgres;Password=postgres";

        services.AddDbContext<AuditDbContext>(options =>
        {
            if (environment.IsEnvironment("Testing"))
            {
                options.UseInMemoryDatabase("AuditEntries_Testing");
            }
            else
            {
                options.UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.SetPostgresVersion(18, 0);
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "audit");
                });
            }
        });
        services.AddScoped<IAuditEntryStore, AuditEntryStore>();

        services.AddGrpcClient<IamService.IamServiceClient>(options =>
            {
                var address = configuration["services:iam-api:http:0"]
                    ?? configuration["services:iam-api:https:0"]
                    ?? "http://iam-api";
                options.Address = new Uri(address);
            })
            .AddServiceDiscovery()
            .AddInterceptor<GrpcJwtHeaderInterceptor>()
            .AddInterceptor<GrpcTraceContextInterceptor>()
            .AddStandardResilienceHandler();
        services.AddScoped<IIamPermissionChecker, IamPermissionChecker>();

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<PermissionDeniedConsumer>();
            bus.AddConsumer<CouponWrittenConsumer>();
            bus.AddConsumer<UserRegisteredConsumer>();
            bus.AddEntityFrameworkOutbox<AuditDbContext>(outbox =>
            {
                outbox.UsePostgres(enableSchemaCaching: false);
                outbox.UseBusOutbox();
                outbox.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });
            bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
                endpoint.UseEntityFrameworkOutbox<AuditDbContext>(context));

            bus.UsingRabbitMq((context, rabbit) =>
            {
                var host = configuration.GetConnectionString("rabbitmq")
                    ?? "amqp://guest:guest@localhost:5672";
                rabbit.Host(new Uri(host));
                rabbit.AutoStart = true;
                rabbit.PrefetchCount = 16;
                rabbit.UseMessageRetry(retry => retry.Exponential(
                    5,
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromSeconds(8),
                    TimeSpan.FromMilliseconds(500)));
                rabbit.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
