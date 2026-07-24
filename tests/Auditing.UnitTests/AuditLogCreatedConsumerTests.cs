using System;
using System.Threading.Tasks;
using Auditing.Api.Consumers;
using Auditing.Api.Data;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Xunit;

namespace Auditing.UnitTests;

public class AuditLogCreatedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldSaveAuditLogToDatabase_WithAllFields()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new AuditingDbContext(options);
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
            "trace-xyz-123",
            timestamp
        );

        var contextMock = new Mock<ConsumeContext<AuditLogCreated>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        var savedLog = await dbContext.AuditLogs.FirstOrDefaultAsync(x => x.Id == messageId, TestContext.Current.CancellationToken);
        savedLog.ShouldNotBeNull();
        savedLog.Id.ShouldBe(messageId);
        savedLog.UserId.ShouldBe("test-user-42");
        savedLog.UserRoles.ShouldBe("Admin,User");
        savedLog.IpAddress.ShouldBe("10.0.0.1");
        savedLog.UserAgent.ShouldBe("Mozilla/5.0");
        savedLog.Action.ShouldBe("Create");
        savedLog.EntityName.ShouldBe("Order");
        savedLog.EntityId.ShouldBe("ORD-999");
        savedLog.Changes.ShouldBe("{\"Total\":100}");
        savedLog.TraceId.ShouldBe("trace-xyz-123");
        savedLog.Timestamp.ShouldBe(timestamp);
    }
}
