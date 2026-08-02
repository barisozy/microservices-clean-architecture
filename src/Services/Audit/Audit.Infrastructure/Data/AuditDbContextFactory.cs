using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Audit.Infrastructure.Data;

public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=audit_db;Username=postgres;Password=postgres",
                npgsql =>
                {
                    npgsql.SetPostgresVersion(18, 0);
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "audit");
                })
            .Options;
        return new AuditDbContext(options);
    }
}
