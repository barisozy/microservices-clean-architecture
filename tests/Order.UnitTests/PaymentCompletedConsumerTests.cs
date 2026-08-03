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

public class PaymentCompletedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldSendMarkOrderAsPaidCommand()
    {
        var orderId = Guid.NewGuid();
        var senderMock = new Mock<ISender>();
        var loggerMock = new Mock<ILogger<PaymentCompletedConsumer>>();

        var consumer = new PaymentCompletedConsumer(senderMock.Object, loggerMock.Object);

        var consumeContextMock = new Mock<ConsumeContext<PaymentCompleted>>();
        consumeContextMock.Setup(x => x.Message).Returns(new PaymentCompleted(orderId, Guid.NewGuid(), "key-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        consumeContextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(consumeContextMock.Object);

        senderMock.Verify(x => x.Send(
            It.Is<MarkOrderAsPaidCommand>(cmd => cmd.OrderId == orderId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
