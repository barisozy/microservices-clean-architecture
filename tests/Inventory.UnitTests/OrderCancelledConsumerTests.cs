using System;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using Inventory.Application.Consumers;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.EntityFrameworkCore;
using Xunit;

namespace Inventory.UnitTests;

public class OrderCancelledConsumerTests
{
    [Fact]
    public async Task Consume_Should_Send_ReleaseStockCommand()
    {
        var mediatorMock = new Mock<IMediator>();
        var dbContextMock = new Mock<Inventory.Application.Common.Interfaces.IInventoryDbContext>();
        
        var orderId = Guid.NewGuid();
        var reservation = new Inventory.Domain.Entities.InventoryReservation { Id = Guid.NewGuid(), OrderId = orderId };
        var reservations = new System.Collections.Generic.List<Inventory.Domain.Entities.InventoryReservation> { reservation };
        dbContextMock.Setup(x => x.Reservations).ReturnsDbSet(reservations);
        
        var consumer = new OrderCancelledConsumer(mediatorMock.Object, dbContextMock.Object);

        var consumeContextMock = new Mock<ConsumeContext<OrderCancelled>>();
        consumeContextMock.Setup(x => x.Message).Returns(new OrderCancelled(orderId, "Reason", DateTimeOffset.UtcNow));
        consumeContextMock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(consumeContextMock.Object);

        mediatorMock.Verify(x => x.Send(It.IsAny<Inventory.Application.Inventory.Commands.ReleaseStockCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
