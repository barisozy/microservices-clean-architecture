using IAM.Infrastructure.Data;
using IAM.Application.Common.Interfaces;
using IAM.Application;
using IAM.Infrastructure.Identity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IAM.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("iam_db")
            ?? configuration.GetConnectionString("IamDb")
            ?? "Host=localhost;Database=iam_db;Username=postgres;Password=postgres";

        services.AddDbContext<IamDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.SetPostgresVersion(18, 0);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "iam");
            });
        });
        services.AddScoped<IIamRepository, IamRepository>();
        services.AddScoped<IPermissionEvaluator, PermissionEvaluator>();

        var valkeyConnectionString = configuration.GetConnectionString("valkey")
            ?? configuration.GetConnectionString("cache")
            ?? "localhost:6379";
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = valkeyConnectionString;
            options.InstanceName = "iam:";
        });

        services.Configure<KeycloakAdminOptions>(configuration.GetSection(KeycloakAdminOptions.SectionName));
        services.PostConfigure<KeycloakAdminOptions>(options =>
        {
            options.BaseUrl = configuration["Keycloak:BaseUrl"] ?? options.BaseUrl;
            options.Realm = configuration["Keycloak:Realm"] ?? options.Realm;
            options.ClientId = configuration["Keycloak:AdminClientId"] ?? options.ClientId;
            options.ClientSecret = configuration["Keycloak:AdminClientSecret"] ?? options.ClientSecret;
        });
        services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KeycloakAdminOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHostedService<IdentityProvisioningWorker>();

        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<IamDbContext>(o =>
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
