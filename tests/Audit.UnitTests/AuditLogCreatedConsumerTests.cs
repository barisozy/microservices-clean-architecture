using Audit.Api.Consumers;
using Audit.Api.Data;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Xunit;

namespace Audit.UnitTests;

public class AuditLogCreatedConsumerTests
{
    private static DbContextOptions<AuditDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task Consume_ShouldAddAuditLogRecordToDatabase()
    {
        // Arrange
        var options = CreateOptions();
        using var dbContext = new AuditDbContext(options);
        var consumer = new AuditLogCreatedConsumer(dbContext);

        var messageId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var message = new AuditLogCreated(
            messageId,
            "test-user-42",
            "Admin,User",
            "10.0.0.1",
            "Mozilla/5.0",
            "Create",
            "Order",
            "ORD-999",
            "{\"Total\":100}",
            "trace-abc-123",
            timestamp
        );

        var contextMock = new Mock<ConsumeContext<AuditLogCreated>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        var savedLog = await dbContext.AuditLogs.FirstOrDefaultAsync(x => x.IdempotencyKey == messageId.ToString(), TestContext.Current.CancellationToken);
        savedLog.ShouldNotBeNull();
        savedLog.IdempotencyKey.ShouldBe(messageId.ToString());
        savedLog.UserId.ShouldBe("test-user-42");
        savedLog.UserRoles.ShouldBe("Admin,User");
        savedLog.IpAddress.ShouldBe("10.0.0.1");
        savedLog.UserAgent.ShouldBe("Mozilla/5.0");
        savedLog.Action.ShouldBe("Create");
        savedLog.EntityName.ShouldBe("Order");
        savedLog.EntityId.ShouldBe("ORD-999");
        savedLog.Changes.ShouldBe("{\"Total\":100}");
        savedLog.TraceId.ShouldBe("trace-abc-123");
        savedLog.Timestamp.ShouldBe(timestamp);
    }
}

