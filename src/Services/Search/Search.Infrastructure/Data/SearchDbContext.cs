using MassTransit;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Search.Domain.Entities;

namespace Search.Infrastructure.Data;

public class SearchDbContext : DbContext
{
    public SearchDbContext(DbContextOptions<SearchDbContext> options) : base(options) { }

    public DbSet<SearchIndex> SearchIndices => Set<SearchIndex>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("search");

        modelBuilder.Entity<SearchIndex>(b =>
        {
            b.HasKey(x => x.Sku);

            b.Property<NpgsqlTsVector>("SearchVector")
                .HasColumnType("tsvector")
                .HasComputedColumnSql(
                    "to_tsvector('english', coalesce(\"Name\", '') || ' ' || coalesce(\"Description\", '') || ' ' || coalesce(\"Sku\", ''))",
                    stored: true);
            b.HasIndex("SearchVector").HasMethod("GIN");
        });

        modelBuilder.AddTransactionalOutboxEntities();
    }
}
