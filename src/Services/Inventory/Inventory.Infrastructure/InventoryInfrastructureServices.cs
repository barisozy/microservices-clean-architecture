using System.Reflection;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Consumers;
using Inventory.Domain.Common;
using Inventory.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Infrastructure.Data
{
    public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options), IInventoryDbContext
    {
        public DbSet<InventoryReservation> Reservations => Set<InventoryReservation>();
        public DbSet<Stock> Stocks => Set<Stock>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema("inventory");
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
            modelBuilder.AddInboxStateEntity();
            modelBuilder.AddOutboxMessageEntity();
            modelBuilder.AddOutboxStateEntity();
        }
    }

    public class ReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
    {
        public void Configure(EntityTypeBuilder<InventoryReservation> builder)
        {
            builder.ToTable("InventoryReservations", "inventory");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Sku).IsRequired().HasMaxLength(100);
        }
    }

    public class StockConfiguration : IEntityTypeConfiguration<Stock>
    {
        public void Configure(EntityTypeBuilder<Stock> builder)
        {
            builder.ToTable("Stocks", "inventory");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Sku).IsRequired().HasMaxLength(100);
            builder.HasIndex(s => s.Sku).IsUnique();
        }
    }

    public class AuditableEntityInterceptor(IUser currentUser) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            UpdateEntities(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            UpdateEntities(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void UpdateEntities(DbContext? context)
        {
            if (context == null) return;
            foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedBy = currentUser.Id;
                    entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                }
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    entry.Entity.LastModifiedBy = currentUser.Id;
                    entry.Entity.LastModifiedAt = DateTimeOffset.UtcNow;
                }
            }
        }
    }

    public class DispatchDomainEventsInterceptor(IMediator mediator) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await DispatchDomainEvents(eventData.Context);
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private async Task DispatchDomainEvents(DbContext? context)
        {
            if (context == null) return;
            var entities = context.ChangeTracker.Entries<BaseEntity>()
                .Where(e => e.Entity.DomainEvents.Any())
                .Select(e => e.Entity).ToList();

            var domainEvents = entities.SelectMany(e => e.DomainEvents).ToList();
            entities.ForEach(e => e.ClearDomainEvents());

            foreach (var domainEvent in domainEvents)
                await mediator.Publish(domainEvent);
        }
    }
}

namespace Inventory.Infrastructure
{
    using Inventory.Infrastructure.Data;
    using System.Security.Claims;

    public class CurrentUser(IHttpContextAccessor httpContextAccessor) : IUser
    {
        public string? Id => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<IUser, CurrentUser>();
            services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
            services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

            services.AddDbContext<InventoryDbContext>((sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                options.UseNpgsql(configuration.GetConnectionString("InventoryDb"), npgsql =>
                {
                    npgsql.SetPostgresVersion(18, 0);
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "inventory");
                });
            });

            services.AddScoped<IInventoryDbContext>(provider => provider.GetRequiredService<InventoryDbContext>());

            services.AddMassTransit(x =>
            {
                x.AddConsumer<OrderCancelledConsumer>();

                x.AddEntityFrameworkOutbox<InventoryDbContext>(o =>
                {
                    o.UsePostgres();
                    o.UseBusOutbox();
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
}

