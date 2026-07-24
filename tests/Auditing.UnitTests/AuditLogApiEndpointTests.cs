using System;
using System.Linq;
using System.Threading.Tasks;
using Auditing.Api.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Auditing.UnitTests;

public class AuditLogApiEndpointTests
{
    private DbContextOptions<AuditingDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task QueryAuditLogs_ShouldFilterByEntityNameAndUserId()
    {
        // Arrange
        var options = CreateOptions();
        using var dbContext = new AuditingDbContext(options);

        dbContext.AuditLogs.AddRange(
            new AuditLogRecord
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                Action = "Create",
                EntityName = "Order",
                EntityId = "1",
                Changes = "{}",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10)
            },
            new AuditLogRecord
            {
                Id = Guid.NewGuid(),
                UserId = "user-2",
                Action = "Create",
                EntityName = "Order",
                EntityId = "2",
                Changes = "{}",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
            },
            new AuditLogRecord
            {
                Id = Guid.NewGuid(),
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
        using var dbContext = new AuditingDbContext(options);

        for (int i = 1; i <= 15; i++)
        {
            dbContext.AuditLogs.Add(new AuditLogRecord
            {
                Id = Guid.NewGuid(),
                UserId = $"user-{i}",
                Action = "Execute",
                EntityName = "Command",
                EntityId = i.ToString(),
                Changes = "{}",
                Timestamp = DateTimeOffset.UtcNow.AddSeconds(i)
            });
        }
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Page 1 with pageSize = 5
        var page1 = await dbContext.AuditLogs
            .OrderByDescending(x => x.Timestamp)
            .Skip(0)
            .Take(5)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Page 2 with pageSize = 5
        var page2 = await dbContext.AuditLogs
            .OrderByDescending(x => x.Timestamp)
            .Skip(5)
            .Take(5)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        page1.Count.ShouldBe(5);
        page2.Count.ShouldBe(5);
        page1.First().EntityId.ShouldBe("15");
        page2.First().EntityId.ShouldBe("10");
    }
}
