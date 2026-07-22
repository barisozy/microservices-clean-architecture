using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using Inventory.Application.Consumers;
using Inventory.Application.Inventory.Commands;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Inventory.UnitTests;

public class OrderCreatedConsumerTests
{
    [Fact]
    public async Task Consume_ShouldPublishStockReserved_WhenReservationSucceeds()
    {
        var senderMock = new Mock<ISender>();
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<OrderCreatedConsumer>>();

        var consumer = new OrderCreatedConsumer(senderMock.Object, publishEndpointMock.Object, loggerMock.Object);

        var orderId = Guid.NewGuid();
        var items = new List<OrderItemContractDto> { new OrderItemContractDto("SKU-1", 5, 10.0m) };
        var message = new OrderCreated(orderId, Guid.NewGuid(), "idempotency-key", items, 50.0m, DateTimeOffset.UtcNow);
        var contextMock = new Mock<ConsumeContext<OrderCreated>>();
        contextMock.Setup(x => x.Message).Returns(message);
        contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        senderMock.Setup(x => x.Send(It.IsAny<ReserveStockCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid.NewGuid(), true, "Success"));

        await consumer.Consume(contextMock.Object);

        publishEndpointMock.Verify(x => x.Publish(It.IsAny<StockReserved>(), It.IsAny<CancellationToken>()), Times.Once);
        publishEndpointMock.Verify(x => x.Publish(It.IsAny<OrderCancelled>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Consume_ShouldPublishOrderCancelled_WhenReservationFails()
    {
        var senderMock = new Mock<ISender>();
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<OrderCreatedConsumer>>();

        var consumer = new OrderCreatedConsumer(senderMock.Object, publishEndpointMock.Object, loggerMock.Object);

        var orderId = Guid.NewGuid();
        var items = new List<OrderItemContractDto> { new OrderItemContractDto("SKU-1", 5, 10.0m) };
        var message = new OrderCreated(orderId, Guid.NewGuid(), "idempotency-key", items, 50.0m, DateTimeOffset.UtcNow);
        var contextMock = new Mock<ConsumeContext<OrderCreated>>();
        contextMock.Setup(x => x.Message).Returns(message);
        contextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        senderMock.Setup(x => x.Send(It.IsAny<ReserveStockCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid.Empty, false, "Failed"));

        await consumer.Consume(contextMock.Object);

        publishEndpointMock.Verify(x => x.Publish(It.IsAny<OrderCancelled>(), It.IsAny<CancellationToken>()), Times.Once);
        publishEndpointMock.Verify(x => x.Publish(It.IsAny<StockReserved>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
