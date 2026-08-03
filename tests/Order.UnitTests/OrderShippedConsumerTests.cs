using System;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Order.Application.Consumers;
using Order.Application.Orders.Commands;
using Xunit;

namespace Order.UnitTests;

public class OrderShippedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldSendMarkOrderAsShippedCommand()
    {
        var orderId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<OrderShippedConsumer>>();

        var consumer = new OrderShippedConsumer(senderMock.Object, loggerMock.Object);

        var consumeContextMock = new Mock<ConsumeContext<OrderShipped>>();
        consumeContextMock.Setup(x => x.Message).Returns(new OrderShipped(orderId, "TRACK-123", DateTimeOffset.UtcNow));
        consumeContextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(consumeContextMock.Object);

        senderMock.Verify(x => x.Send(
            It.Is<MarkOrderAsShippedCommand>(cmd => cmd.OrderId == orderId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
