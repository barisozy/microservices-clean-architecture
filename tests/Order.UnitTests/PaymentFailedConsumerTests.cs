using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.EntityFrameworkCore;
using Order.Application.Common.Interfaces;
using Order.Application.Consumers;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public class PaymentFailedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldCancelOrderAndPublishEvent_WhenOrderExists()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = new global::Order.Domain.Entities.Order { Id = orderId, BuyerId = "buyer-1" };
        var dbContextMock = new Mock<IOrderWriteRepository>();
        dbContextMock.Setup(x => x.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var publishMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<PaymentFailedConsumer>>();

        var consumer = new PaymentFailedConsumer(dbContextMock.Object, publishMock.Object, loggerMock.Object);

        var consumeContextMock = new Mock<ConsumeContext<PaymentFailed>>();
        consumeContextMock.Setup(x => x.Message).Returns(new PaymentFailed(orderId, "key-1", "Card declined", DateTimeOffset.UtcNow));
        consumeContextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(consumeContextMock.Object);

        // Assert
        order.Status.ToString().ShouldBe("Cancelled");
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publishMock.Verify(x => x.Publish(It.Is<OrderCancelled>(e => e.OrderId == orderId && e.Reason == "Card declined"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_ShouldDoNothing_WhenOrderNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var dbContextMock = new Mock<IOrderWriteRepository>();
        dbContextMock.Setup(x => x.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((global::Order.Domain.Entities.Order?)null);

        var publishMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<PaymentFailedConsumer>>();

        var consumer = new PaymentFailedConsumer(dbContextMock.Object, publishMock.Object, loggerMock.Object);

        var consumeContextMock = new Mock<ConsumeContext<PaymentFailed>>();
        consumeContextMock.Setup(x => x.Message).Returns(new PaymentFailed(orderId, "key-1", "Card declined", DateTimeOffset.UtcNow));
        consumeContextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(consumeContextMock.Object);

        // Assert
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        publishMock.Verify(x => x.Publish(It.IsAny<OrderCancelled>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}


