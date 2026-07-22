using System;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Ordering.Application.Consumers;
using Ordering.Application.Orders.Commands.CancelOrder;
using Xunit;

namespace Ordering.UnitTests;

public class StockReleasedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldSendCancelOrderCommand()
    {
        var senderMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<StockReleasedConsumer>>();

        var consumer = new StockReleasedConsumer(senderMock.Object, loggerMock.Object);

        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var message = new StockReleased(orderId, reservationId, DateTimeOffset.UtcNow);
        
        var contextMock = new Mock<ConsumeContext<StockReleased>>();
        contextMock.Setup(x => x.Message).Returns(message);
        contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        senderMock.Verify(x => x.Send(
            It.Is<CancelOrderCommand>(cmd => cmd.OrderId == orderId && cmd.Reason == "Saga Rollback: Stock Released (Payment Failed)"), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
