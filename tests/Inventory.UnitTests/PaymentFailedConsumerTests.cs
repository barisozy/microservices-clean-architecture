using ECommerce.Contracts.Events.v1;
using Inventory.Application.Consumers;
using Inventory.Application.Inventory.Commands;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Inventory.UnitTests;

public class PaymentFailedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldSendReleaseOrderStockCommand()
    {
        var sender = new Mock<ISender>();
        var logger = new Mock<ILogger<PaymentFailedConsumer>>();
        var consumer = new PaymentFailedConsumer(sender.Object, logger.Object);
        var orderId = Guid.NewGuid();
        var context = new Mock<ConsumeContext<PaymentFailed>>();

        context.Setup(x => x.Message).Returns(new PaymentFailed(
            orderId,
            "customer-1",
            "Payment declined",
            DateTimeOffset.UtcNow));
        context.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(context.Object);

        sender.Verify(
            x => x.Send(
                It.Is<ReleaseOrderStockCommand>(command => command.OrderId == orderId),
                CancellationToken.None),
            Times.Once);
    }
}
