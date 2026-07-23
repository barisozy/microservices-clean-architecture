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
    public async Task Consume_ShouldSaveAuditLogToDatabase()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(databaseName: "AuditDb_Test")
            .Options;

        using var dbContext = new AuditingDbContext(options);
        var consumer = new AuditLogCreatedConsumer(dbContext);

        var message = new AuditLogCreated(
            Guid.NewGuid(),
            "test-user",
            "Admin",
            "127.0.0.1",
            "Agent",
            "Create",
            "Order",
            "123",
            "{}",
            "trace-1",
            DateTimeOffset.UtcNow
        );

        var contextMock = new Mock<ConsumeContext<AuditLogCreated>>();
        contextMock.Setup(x => x.Message).Returns(message);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        var savedLog = await dbContext.AuditLogs.FirstOrDefaultAsync(x => x.Id == message.Id);
        savedLog.ShouldNotBeNull();
        savedLog.UserId.ShouldBe("test-user");
        savedLog.EntityName.ShouldBe("Order");
    }
}
