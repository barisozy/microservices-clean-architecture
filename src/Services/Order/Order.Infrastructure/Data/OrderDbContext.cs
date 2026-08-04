using MassTransit;
using Microsoft.EntityFrameworkCore;
using Order.Application.Common.Interfaces;
using Order.Domain.Entities;
using Order.Application.Checkout;
using Order.Domain.Enums;

namespace Order.Infrastructure.Data;

public class OrderDbContext : DbContext, IOrderWriteRepository
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

    public Task<global::Order.Domain.Entities.Order?> FindByIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Orders.FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);

    public Task<OrderStatus?> GetStatusAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Orders.AsNoTracking().Where(order => order.Id == orderId)
            .Select(order => (OrderStatus?)order.Status).SingleOrDefaultAsync(cancellationToken);

    public void Add(global::Order.Domain.Entities.Order order) => Orders.Add(order);

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
