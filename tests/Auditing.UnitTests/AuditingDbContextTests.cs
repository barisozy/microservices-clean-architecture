using System;
using System.Linq;
using System.Threading.Tasks;
using Auditing.Api.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Auditing.UnitTests;

public class AuditingDbContextTests
{
    [Fact]
    public async Task OnModelCreating_ShouldConfigureIndexesAndSchema()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new AuditingDbContext(options);

        // Act
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Assert
        var entityType = dbContext.Model.FindEntityType(typeof(AuditLogRecord));
        entityType.ShouldNotBeNull();
        
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
        var id = Guid.NewGuid();
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
