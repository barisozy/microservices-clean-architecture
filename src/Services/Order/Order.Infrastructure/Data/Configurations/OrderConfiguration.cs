using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Domain.Entities;

namespace Order.Infrastructure.Data.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<global::Order.Domain.Entities.Order>
{
    public void Configure(EntityTypeBuilder<global::Order.Domain.Entities.Order> builder)
    {
        builder.ToTable("Orders", "order");
        builder.HasKey(order => order.Id);
        builder.Property(order => order.BuyerId).IsRequired().HasMaxLength(200);
        builder.Property(order => order.CustomerId).IsRequired();
        builder.Property(order => order.KeycloakSubject).IsRequired();
        builder.Property(order => order.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(order => order.IdempotencyKey).IsRequired().HasMaxLength(100);
        builder.HasIndex(order => new { order.CustomerId, order.IdempotencyKey }).IsUnique();
        builder.HasMany(order => order.OrderItems).WithOne().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
