using Audit.Api.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Audit.UnitTests;

public class AuditLogApiEndpointTests
{
    private static DbContextOptions<AuditDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task QueryAuditLogs_ShouldFilterByEntityNameAndUserId()
    {
        // Arrange
        var options = CreateOptions();
        using var dbContext = new AuditDbContext(options);

        dbContext.AuditLogs.AddRange(
            new AuditLogRecord
            {
                UserId = "user-1",
                Action = "Create",
                EntityName = "Order",
                EntityId = "1",
                Changes = "{}",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10)
            },
            new AuditLogRecord
            {
                UserId = "user-2",
                Action = "Create",
                EntityName = "Order",
                EntityId = "2",
                Changes = "{}",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
            },
            new AuditLogRecord
            {
                UserId = "user-1",
                Action = "Update",
                EntityName = "Product",
                EntityId = "10",
                Changes = "{}",
                Timestamp = DateTimeOffset.UtcNow
            }
        );
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - Filter by entityName
        var entityFiltered = await dbContext.AuditLogs
            .Where(x => x.EntityName == "Order")
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync(TestContext.Current.CancellationToken);

        entityFiltered.Count.ShouldBe(2);

        // Act & Assert - Filter by userId
        var userFiltered = await dbContext.AuditLogs
            .Where(x => x.UserId == "user-1")
            .OrderByDescending(x => x.Timestamp)
            .ToListAsync(TestContext.Current.CancellationToken);

        userFiltered.Count.ShouldBe(2);

        // Act & Assert - Filter by both
        var bothFiltered = await dbContext.AuditLogs
            .Where(x => x.EntityName == "Order" && x.UserId == "user-1")
            .ToListAsync(TestContext.Current.CancellationToken);

        bothFiltered.Count.ShouldBe(1);
        bothFiltered.First().EntityId.ShouldBe("1");
    }

    [Fact]
    public async Task QueryAuditLogs_ShouldPaginateCorrectly()
    {
        // Arrange
        var options = CreateOptions();
        using var dbContext = new AuditDbContext(options);

        for (int i = 1; i <= 15; i++)
        {
            dbContext.AuditLogs.Add(new AuditLogRecord
            {
                UserId = $"user-{i}",
                Action = "Execute",
                EntityName = "Command",
                EntityId = i.ToString(),
                Changes = "{}",
                Timestamp = DateTimeOffset.UtcNow.AddSeconds(i)
            });
        }
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Page 1 (size 10)
        var page1 = await dbContext.AuditLogs
            .OrderBy(x => x.Timestamp)
            .Skip(0)
            .Take(10)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Act - Page 2 (size 10)
        var page2 = await dbContext.AuditLogs
            .OrderBy(x => x.Timestamp)
            .Skip(10)
            .Take(10)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        page1.Count.ShouldBe(10);
        page2.Count.ShouldBe(5);
        page1.First().EntityId.ShouldBe("1");
        page2.First().EntityId.ShouldBe("11");
    }
}

