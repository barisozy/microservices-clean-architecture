using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Order.Application.Checkout;

namespace Order.Infrastructure.Data.Configurations;

public sealed class CheckoutStateConfiguration : IEntityTypeConfiguration<CheckoutState>
{
    public void Configure(EntityTypeBuilder<CheckoutState> builder)
    {
        builder.ToTable("CheckoutStates", "order");
        builder.HasKey(x => x.CorrelationId);
        builder.Property(x => x.CurrentState).HasMaxLength(64).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.Version).HasColumnName("xmin").IsRowVersion();
        builder.Property(x => x.ItemsJson).HasColumnType("jsonb");
    }
}
