using Catalog.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<ProductVariant> Variants => Set<ProductVariant>();
    public DbSet<ProductImage> Images => Set<ProductImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("catalog");

        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Sku).IsUnique();
        });

        modelBuilder.Entity<Category>(b => b.HasKey(x => x.Id));
        modelBuilder.Entity<Brand>(b => b.HasKey(x => x.Id));
        modelBuilder.Entity<ProductVariant>(b => b.HasKey(x => x.Id));
        modelBuilder.Entity<ProductImage>(b => b.HasKey(x => x.Id));

        modelBuilder.AddTransactionalOutboxEntities();
    }
}
