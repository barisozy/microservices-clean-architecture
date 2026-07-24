using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Auditing.Api.Data;

public class AuditLogRecord
{
    [Key]
    public Guid Id { get; set; }
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
}

public class AuditingDbContext(DbContextOptions<AuditingDbContext> options) : DbContext(options)
{
    public DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AuditLogRecord>().ToTable("AuditLogs", "auditing");
        modelBuilder.Entity<AuditLogRecord>().HasIndex(x => x.Timestamp);
        modelBuilder.Entity<AuditLogRecord>().HasIndex(x => x.EntityId);
        modelBuilder.Entity<AuditLogRecord>().HasIndex(x => x.UserId);
    }
}
