using Audit.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Audit.Infrastructure.Data;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("audit");

        modelBuilder.Entity<AuditEntry>(builder =>
        {
            builder.ToTable("AuditEntries");
            builder.HasKey(entry => entry.Id);
            builder.Property(entry => entry.Id).ValueGeneratedOnAdd();
            builder.Property(entry => entry.ActorSubject).IsRequired().HasMaxLength(200);
            builder.Property(entry => entry.Action).IsRequired().HasMaxLength(200);
            builder.Property(entry => entry.TargetType).IsRequired().HasMaxLength(100);
            builder.Property(entry => entry.TargetId).IsRequired().HasMaxLength(300);
            builder.Property(entry => entry.Outcome).IsRequired().HasMaxLength(32);
            builder.Property(entry => entry.OccurredAt).IsRequired();
            builder.Property(entry => entry.RowHash).IsRequired().HasColumnType("char(64)");
            builder.Property(entry => entry.PrevHash).IsRequired().HasColumnType("char(64)");
            builder.HasIndex(entry => entry.IdempotencyKey).IsUnique();
            builder.HasIndex(entry => new { entry.ActorSubject, entry.Action, entry.OccurredAt });
        });

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureAppendOnly()
    {
        if (ChangeTracker.Entries<AuditEntry>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Audit entries are append-only and cannot be updated or deleted.");
        }
    }
}
