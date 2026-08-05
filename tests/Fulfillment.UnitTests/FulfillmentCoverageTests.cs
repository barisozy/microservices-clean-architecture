using ECommerce.Contracts.Events.v1;
using Fulfillment.Application.Common.Interfaces;
using Fulfillment.Application.Consumers;
using Fulfillment.Application.Fulfillment.Queries;
using Fulfillment.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Fulfillment.UnitTests;

public class FulfillmentCoverageTests
{
    [Fact]
    public async Task GetFulfillmentTaskHandler_ReturnsRepositoryValue()
    {
        var orderId = Guid.NewGuid();
        var repository = new Mock<IFulfillmentReadRepository>();
        repository.Setup(x => x.GetFulfillmentStatusAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync("Shipped");

        var result = await new GetFulfillmentTaskQueryHandler(repository.Object).Handle(new GetFulfillmentTaskQuery(orderId), CancellationToken.None);

        result.ShouldBe("Shipped");
    }

    [Fact]
    public async Task PaymentCompletedConsumer_ReusesExistingShipmentButCreatesTask()
    {
        var orderId = Guid.NewGuid();
        var tasks = new List<FulfillmentTask>();
        var shipments = new List<Shipment> { new() { OrderId = orderId, TrackingNumber = "EXISTING" } };
        var db = new Mock<IFulfillmentWriteRepository>();
        db.Setup(x => x.Add(It.IsAny<FulfillmentTask>())).Callback<FulfillmentTask>(tasks.Add);
        db.Setup(x => x.Add(It.IsAny<Shipment>())).Callback<Shipment>(shipments.Add);
        db.Setup(x => x.FindShipmentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid oId, CancellationToken token) => shipments.Find(x => x.OrderId == oId));
        var readRepository = new Mock<IFulfillmentReadRepository>();
        var publisher = new Mock<IPublishEndpoint>();
        var consumer = new PaymentCompletedConsumer(db.Object, publisher.Object, Mock.Of<ILogger<PaymentCompletedConsumer>>(), readRepository.Object);
        var context = new Mock<ConsumeContext<PaymentCompleted>>();
        context.SetupGet(x => x.Message).Returns(new PaymentCompleted(orderId, Guid.NewGuid(), "key", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(context.Object);

        tasks.Count.ShouldBe(1);
        shipments.Count.ShouldBe(1);
        readRepository.Verify(x => x.SetFulfillmentStatusAsync(orderId, "Shipped", It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(x => x.Publish(It.Is<OrderShipped>(e => e.OrderId == orderId), It.IsAny<CancellationToken>()), Times.Once);
    }
}
