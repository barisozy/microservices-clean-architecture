using MassTransit;
using Microsoft.EntityFrameworkCore;
using Promotion.Domain.Entities;

namespace Promotion.Infrastructure.Data;

public class PromotionDbContext : DbContext
{
    public PromotionDbContext(DbContextOptions<PromotionDbContext> options) : base(options) { }

    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("promotion");

        modelBuilder.Entity<Coupon>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Campaign>(b => b.HasKey(x => x.Id));

        modelBuilder.AddTransactionalOutboxEntities();
    }
}
