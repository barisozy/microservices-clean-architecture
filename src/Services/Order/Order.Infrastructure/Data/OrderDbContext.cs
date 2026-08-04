using MassTransit;
using Microsoft.EntityFrameworkCore;
using Order.Application.Common.Interfaces;
using Order.Domain.Entities;
using Order.Application.Checkout;

namespace Order.Infrastructure.Data;

public class OrderDbContext : DbContext, IOrderDbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

        public DbSet<global::Order.Domain.Entities.Order> Orders => Set<global::Order.Domain.Entities.Order>();
        public DbSet<CheckoutState> CheckoutStates => Set<CheckoutState>();

    public Task<global::Order.Domain.Entities.Order?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        Orders.FirstOrDefaultAsync(order => order.IdempotencyKey == idempotencyKey, cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("order");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderDbContext).Assembly);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
