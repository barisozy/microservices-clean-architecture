using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.EntityFrameworkCore;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.Commands;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public class OrderCommandHandlerTests
{
    [Fact]
    public async Task MarkOrderAsPaid_WhenOrderExists_ShouldMarkAsPaidAndSave()
    {
        var orderId = Guid.NewGuid();
        var order = global::Order.Domain.Entities.Order.Create("buyer-1", "key-1", new List<OrderItem>());
        order.GetType().GetProperty(nameof(Order.Domain.Entities.Order.Id))!.SetValue(order, orderId);

        var dbContextMock = new Mock<IOrderDbContext>();
        dbContextMock.Setup(x => x.Orders).ReturnsDbSet(new List<global::Order.Domain.Entities.Order> { order });

        var handler = new MarkOrderAsPaidCommandHandler(dbContextMock.Object);

        await handler.Handle(new MarkOrderAsPaidCommand(orderId), CancellationToken.None);

        order.Status.ShouldBe(OrderStatus.Paid);
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkOrderAsPaid_WhenOrderNotFound_ShouldThrowInvalidOperationException()
    {
        var dbContextMock = new Mock<IOrderDbContext>();
        dbContextMock.Setup(x => x.Orders).ReturnsDbSet(new List<global::Order.Domain.Entities.Order>());

        var handler = new MarkOrderAsPaidCommandHandler(dbContextMock.Object);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.Handle(new MarkOrderAsPaidCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task MarkOrderAsShipped_WhenOrderExistsAndPaid_ShouldMarkAsShippedAndSave()
    {
        var orderId = Guid.NewGuid();
        var order = global::Order.Domain.Entities.Order.Create("buyer-1", "key-1", new List<OrderItem>());
        order.GetType().GetProperty(nameof(Order.Domain.Entities.Order.Id))!.SetValue(order, orderId);
        order.MarkAsPaid();

        var dbContextMock = new Mock<IOrderDbContext>();
        dbContextMock.Setup(x => x.Orders).ReturnsDbSet(new List<global::Order.Domain.Entities.Order> { order });

        var handler = new MarkOrderAsShippedCommandHandler(dbContextMock.Object);

        await handler.Handle(new MarkOrderAsShippedCommand(orderId), CancellationToken.None);

        order.Status.ShouldBe(OrderStatus.Shipped);
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkOrderAsShipped_WhenOrderNotFound_ShouldThrowInvalidOperationException()
    {
        var dbContextMock = new Mock<IOrderDbContext>();
        dbContextMock.Setup(x => x.Orders).ReturnsDbSet(new List<global::Order.Domain.Entities.Order>());

        var handler = new MarkOrderAsShippedCommandHandler(dbContextMock.Object);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.Handle(new MarkOrderAsShippedCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
