using Audit.Api.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Audit.UnitTests;

public class AuditDbContextTests
{
    private static DbContextOptions<AuditDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public void OnModelCreating_ShouldConfigureAuditLogsTableAndIndexes()
    {
        // Arrange
        var options = CreateOptions();
        using var dbContext = new AuditDbContext(options);

        // Act
        var model = dbContext.Model;
        var entityType = model.FindEntityType(typeof(AuditLogRecord));

        // Assert
        entityType.ShouldNotBeNull();
        entityType.GetTableName().ShouldBe("AuditLogs");
        entityType.GetSchema().ShouldBe("Audit");

        // Verify indexes
        var indexes = entityType.GetIndexes().ToList();
        indexes.Any(i => i.Properties.Any(p => p.Name == nameof(AuditLogRecord.Timestamp))).ShouldBeTrue();
        indexes.Any(i => i.Properties.Any(p => p.Name == nameof(AuditLogRecord.EntityId))).ShouldBeTrue();
        indexes.Any(i => i.Properties.Any(p => p.Name == nameof(AuditLogRecord.UserId))).ShouldBeTrue();
    }

    [Fact]
    public void AuditLogRecord_PropertyGettersAndSetters_ShouldWorkCorrectly()
    {
        // Arrange & Act
        long id = 1L;
        var now = DateTimeOffset.UtcNow;
        var record = new AuditLogRecord
        {
            Id = id,
            UserId = "user-1",
            UserRoles = "Admin",
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent",
            Action = "Update",
            EntityName = "Product",
            EntityId = "P-100",
            Changes = "{}",
            TraceId = "trace-123",
            Timestamp = now
        };

        // Assert
        record.Id.ShouldBe(id);
        record.UserId.ShouldBe("user-1");
        record.UserRoles.ShouldBe("Admin");
        record.IpAddress.ShouldBe("127.0.0.1");
        record.UserAgent.ShouldBe("TestAgent");
        record.Action.ShouldBe("Update");
        record.EntityName.ShouldBe("Product");
        record.EntityId.ShouldBe("P-100");
        record.Changes.ShouldBe("{}");
        record.TraceId.ShouldBe("trace-123");
        record.Timestamp.ShouldBe(now);
    }
}

