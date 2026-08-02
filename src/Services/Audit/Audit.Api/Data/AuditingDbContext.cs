using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace Audit.Api.Data;

public class AuditLogRecord
{
    [Key]
    public long Id { get; set; }
    public required string UserId { get; set; }
    public string? UserRoles { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public required string Action { get; set; }
    public required string EntityName { get; set; }
    public required string EntityId { get; set; }
    public required string Changes { get; set; }
    public string? TraceId { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    // Sprint 9: SHA-256 Tamper-evident Hash Chain
    public string RowHash { get; set; } = string.Empty;
    public string PrevHash { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;

    public string CalculateHash()
    {
        var rawData = $"{PrevHash}:{UserId}:{Action}:{EntityName}:{EntityId}:{Changes}:{Timestamp.ToUnixTimeMilliseconds()}:{IdempotencyKey}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexStringLower(bytes);
    }
}

public class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AuditLogRecord>().ToTable("AuditLogs", "Audit");
        modelBuilder.Entity<AuditLogRecord>().HasIndex(x => x.Timestamp);
        modelBuilder.Entity<AuditLogRecord>().HasIndex(x => x.EntityId);
        modelBuilder.Entity<AuditLogRecord>().HasIndex(x => x.UserId);
        modelBuilder.Entity<AuditLogRecord>().HasIndex(x => x.IdempotencyKey).IsUnique();
    }
}

