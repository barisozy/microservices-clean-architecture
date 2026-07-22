using System.Security.Claims;
using Fulfillment.Application.Common.Interfaces;
using Fulfillment.Application.Consumers;
using Fulfillment.Domain.Entities;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fulfillment.Infrastructure;

public class FulfillmentDbContext : DbContext, IFulfillmentDbContext
{
    public FulfillmentDbContext(DbContextOptions<FulfillmentDbContext> options) : base(options)
    {
    }

    public DbSet<FulfillmentTask> Tasks => Set<FulfillmentTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("fulfillment");
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}

public class CurrentUser : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Id => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUser, CurrentUser>();

        services.AddDbContext<FulfillmentDbContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("FulfillmentDb"), npgsql =>
            {
                npgsql.SetPostgresVersion(18, 0);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "fulfillment");
            });
        });

        services.AddScoped<IFulfillmentDbContext>(provider => provider.GetRequiredService<FulfillmentDbContext>());

        var valkeyConnectionString = configuration.GetConnectionString("valkey")
            ?? configuration.GetConnectionString("cache")
            ?? "localhost:6379";
        services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ => StackExchange.Redis.ConnectionMultiplexer.Connect(valkeyConnectionString));
        services.AddScoped<Fulfillment.Application.Common.Interfaces.IFulfillmentReadRepository, Fulfillment.Infrastructure.Data.Repositories.FulfillmentReadRepository>();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<PaymentCompletedConsumer>();

            x.AddEntityFrameworkOutbox<FulfillmentDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitConnectionString = configuration.GetConnectionString("rabbitmq") ?? "amqp://guest:guest@localhost:5672";
                cfg.Host(new Uri(rabbitConnectionString));
                
                // Event Resilience Patterns: Retry policy, Dead letter queue, Poison message handling
                // 1. Retry policy (Retry x3)
                cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                
                // 2 & 3. Dead letter queue (DLQ) & Poison message handling
                // MassTransit automatically moves messages that fail all retries to a fault/DLQ queue.
                
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
