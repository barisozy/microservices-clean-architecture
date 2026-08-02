using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Notification.Infrastructure.Consumers;
using Notification.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace Notification.UnitTests;

public class NotificationConsumerTests
{
    [Fact]
    public async Task Consumers_ShouldPersistShippingAndPaymentFailureNotifications()
    {
        await using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var shipped = new Mock<ConsumeContext<OrderShipped>>();
        shipped.SetupGet(x => x.Message).Returns(new OrderShipped(Guid.CreateVersion7(), "TRACK-1", DateTimeOffset.UtcNow));
        var failed = new Mock<ConsumeContext<PaymentFailed>>();
        failed.SetupGet(x => x.Message).Returns(new PaymentFailed(Guid.CreateVersion7(), "key", "Declined", DateTimeOffset.UtcNow));

        await new OrderShippedConsumer(db, NullLogger<OrderShippedConsumer>.Instance).Consume(shipped.Object);
        await new PaymentFailedConsumer(db, NullLogger<PaymentFailedConsumer>.Instance).Consume(failed.Object);

        db.Logs.Count().ShouldBe(2);
        db.Logs.ShouldContain(log => log.EventType == "OrderShipped" && log.Content.Contains("TRACK-1"));
        db.Logs.ShouldContain(log => log.EventType == "PaymentFailed" && log.Content.Contains("Declined"));
    }
}
