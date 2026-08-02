using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.EntityFrameworkCore;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.Commands.CancelOrder;
using Order.Domain.Entities;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public class CancelOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_Cancel_Order_And_Publish()
    {
        var contextMock = new Mock<IOrderDbContext>();
        var publishMock = new Mock<IPublishEndpoint>();

        var order = new global::Order.Domain.Entities.Order { Id = Guid.NewGuid(), BuyerId = "b1" };
        contextMock.Setup(x => x.Orders).ReturnsDbSet(new List<global::Order.Domain.Entities.Order> { order });

        var handler = new CancelOrderCommandHandler(contextMock.Object, publishMock.Object);
        var result = await handler.Handle(new CancelOrderCommand(order.Id, "Reason"), CancellationToken.None);

        result.ShouldBeTrue();
        order.Status.ToString().ShouldBe("Cancelled");
        contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publishMock.Verify(x => x.Publish(It.IsAny<OrderCancelled>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnFalse_WhenOrderNotFound()
    {
        var contextMock = new Mock<IOrderDbContext>();
        var publishMock = new Mock<IPublishEndpoint>();

        contextMock.Setup(x => x.Orders).ReturnsDbSet(new List<global::Order.Domain.Entities.Order>());

        var handler = new CancelOrderCommandHandler(contextMock.Object, publishMock.Object);
        var result = await handler.Handle(new CancelOrderCommand(Guid.NewGuid(), "Reason"), CancellationToken.None);

        result.ShouldBeFalse();
        contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        publishMock.Verify(x => x.Publish(It.IsAny<OrderCancelled>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
