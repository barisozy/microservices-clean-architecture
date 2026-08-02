using System.Security.Claims;
using ECommerce.Auditing;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payment.Application.Common.Interfaces;
using Payment.Application.Consumers;
using Payment.Domain.Entities;

namespace Payment.Infrastructure.Data
{
    public class PaymentDbContext : DbContext, IPaymentDbContext
    {
        public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
        {
        }

        public DbSet<PaymentRecord> Payment => Set<PaymentRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema("payment");
            // Plan Sprint 1: IdempotencyKey UNIQUE constraint on Payment
            modelBuilder.Entity<PaymentRecord>()
                .HasIndex(p => p.IdempotencyKey)
                .IsUnique();
            modelBuilder.AddInboxStateEntity();
            modelBuilder.AddOutboxMessageEntity();
            modelBuilder.AddOutboxStateEntity();
        }
    }
}

namespace Payment.Infrastructure
{
    using Payment.Infrastructure.Data;

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
            services.AddECommerceAuditing();

            services.AddDbContext<PaymentDbContext>((sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor>());
                options.UseNpgsql(configuration.GetConnectionString("PaymentDb"), npgsql =>
                {
                    npgsql.SetPostgresVersion(18, 0);
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "payment");
                });
            });

            services.AddScoped<IPaymentDbContext>(provider => provider.GetRequiredService<PaymentDbContext>());

            var valkeyConnectionString = configuration.GetConnectionString("valkey")
                ?? configuration.GetConnectionString("cache")
                ?? "localhost:6379";
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ => StackExchange.Redis.ConnectionMultiplexer.Connect(valkeyConnectionString));
            services.AddScoped<Payment.Application.Common.Interfaces.IPaymentReadRepository, Payment.Infrastructure.Data.Repositories.PaymentReadRepository>();

            services.AddMassTransit(x =>
            {
                x.AddConsumer<OrderCreatedConsumer>();

                x.AddEntityFrameworkOutbox<PaymentDbContext>(o =>
                {
                    o.UsePostgres(enableSchemaCaching: false);
                    o.UseBusOutbox();
                    o.QueryDelay = TimeSpan.FromSeconds(1);
                    o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
                });
                x.AddConfigureEndpointsCallback((context, _, endpoint) =>
                    endpoint.UseEntityFrameworkOutbox<PaymentDbContext>(context));

                x.UsingRabbitMq((context, cfg) =>
                {
                    var rabbitConnectionString = configuration.GetConnectionString("rabbitmq") ?? "amqp://guest:guest@localhost:5672";
                    cfg.Host(new Uri(rabbitConnectionString));
                    cfg.AutoStart = true;
                    
                    // Event Resilience Patterns: Retry policy, Dead letter queue, Poison message handling
                    // 1. Retry policy (Retry x3)
                    cfg.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(8), TimeSpan.FromMilliseconds(500)));
                    
                    // 2 & 3. Dead letter queue (DLQ) & Poison message handling
                    // MassTransit automatically moves messages that fail all retries to a fault/DLQ queue.
                    
                    cfg.ConfigureEndpoints(context);
                });
            });

            return services;
        }
    }
}

