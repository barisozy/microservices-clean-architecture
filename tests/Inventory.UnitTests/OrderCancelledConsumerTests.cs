using ECommerce.Contracts.Events.v1;
using Inventory.Application.Consumers;
using Inventory.Application.Inventory.Commands;
using MassTransit;
using MediatR;
using Moq;
using Xunit;

namespace Inventory.UnitTests;

public class OrderCancelledConsumerTests
{
    [Fact]
    public async Task Consume_ShouldSendReleaseOrderStockCommand()
    {
        var sender = new Mock<ISender>();
        var consumer = new OrderCancelledConsumer(sender.Object);
        var orderId = Guid.NewGuid();
        var context = new Mock<ConsumeContext<OrderCancelled>>();

        context.Setup(x => x.Message).Returns(new OrderCancelled(
            orderId,
            "Payment failed",
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
