using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Order.Infrastructure.Data;

public sealed class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=order_db;Username=postgres;Password=postgres",
                npgsql =>
                {
                    npgsql.SetPostgresVersion(18, 0);
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "order");
                })
            .Options;

        return new OrderDbContext(options);
    }
}
