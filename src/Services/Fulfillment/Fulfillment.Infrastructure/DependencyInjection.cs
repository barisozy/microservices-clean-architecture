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
        modelBuilder.HasDefaultSchema("fulfillment");
        base.OnModelCreating(modelBuilder);
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
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
