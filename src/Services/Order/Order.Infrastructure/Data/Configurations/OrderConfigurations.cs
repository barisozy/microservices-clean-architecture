using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Domain.Entities;

namespace Order.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<global::Order.Domain.Entities.Order>
{
    public void Configure(EntityTypeBuilder<global::Order.Domain.Entities.Order> builder)
    {
        builder.ToTable("Orders", "order");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.BuyerId).IsRequired().HasMaxLength(200);
        // Plan Sprint 1: IdempotencyKey UNIQUE constraint — duplicate key returns original OrderId
        builder.Property(o => o.IdempotencyKey).IsRequired().HasMaxLength(100);
        builder.HasIndex(o => o.IdempotencyKey).IsUnique();
        builder.HasMany(o => o.OrderItems).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", "order");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Sku).IsRequired().HasMaxLength(100);
    }
}
