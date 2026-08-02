using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Search.Infrastructure.Consumers;
using Search.Infrastructure.Data;
using Search.Application;

namespace Search.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("search_db")
            ?? configuration.GetConnectionString("SearchDb")
            ?? "Host=localhost;Database=search_db;Username=postgres;Password=postgres";

        services.AddDbContext<SearchDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.SetPostgresVersion(18, 0);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "search");
            });
        });
        services.AddScoped<ISearchReadRepository, SearchReadRepository>();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<ProductUpsertedConsumer>();

            x.AddEntityFrameworkOutbox<SearchDbContext>(o =>
            {
                o.UsePostgres(enableSchemaCaching: false);
                o.UseBusOutbox();
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });
            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
                endpoint.UseEntityFrameworkOutbox<SearchDbContext>(context));

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
