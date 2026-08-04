using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.EventHandlers;
using Order.Application.Orders.Queries;
using Order.Domain.Events;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public class OrderReadModelUpdaterTests
{
    [Fact]
    public async Task Handle_OrderCreatedDomainEvent_ShouldUpdateReadRepository()
    {
        // Arrange
        var readRepoMock = new Mock<IOrderReadRepository>();
        var updater = new OrderReadModelUpdater(readRepoMock.Object);
        var order = new global::Order.Domain.Entities.Order { Id = Guid.NewGuid(), BuyerId = "buyer-123" };
        var evt = new OrderCreatedDomainEvent(order);

        // Act
        await updater.Handle(evt, CancellationToken.None);

        // Assert
        readRepoMock.Verify(x => x.SetOrderAsync(
            It.Is<OrderStatusDto>(d => d.Id == order.Id && d.BuyerId == "buyer-123" && d.Status == "PendingInventory"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OrderCancelledDomainEvent_ShouldUpdateReadRepository()
    {
        // Arrange
        var readRepoMock = new Mock<IOrderReadRepository>();
        var updater = new OrderReadModelUpdater(readRepoMock.Object);
        var order = new global::Order.Domain.Entities.Order { Id = Guid.NewGuid(), BuyerId = "buyer-123" };
        order.Cancel("Out of stock");
        var evt = new OrderCancelledDomainEvent(order, "Out of stock");

        // Act
        await updater.Handle(evt, CancellationToken.None);

        // Assert
        readRepoMock.Verify(x => x.SetOrderAsync(
            It.Is<OrderStatusDto>(d => d.Id == order.Id && d.BuyerId == "buyer-123" && d.Status == "Cancelled"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OrderPaidDomainEvent_ShouldUpdateReadRepositoryWithPaidStatus()
    {
        var readRepoMock = new Mock<IOrderReadRepository>();
        var updater = new OrderReadModelUpdater(readRepoMock.Object);
        var order = global::Order.Domain.Entities.Order.Create("buyer-123", "key-123", []);
        order.MarkAsPaid();

        await updater.Handle(new OrderPaidDomainEvent(order), CancellationToken.None);

        readRepoMock.Verify(x => x.SetOrderAsync(
            It.Is<OrderStatusDto>(d => d.Id == order.Id && d.BuyerId == "buyer-123" && d.Status == "Paid"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OrderShippedDomainEvent_ShouldUpdateReadRepositoryWithShippedStatus()
    {
        var readRepoMock = new Mock<IOrderReadRepository>();
        var updater = new OrderReadModelUpdater(readRepoMock.Object);
        var order = global::Order.Domain.Entities.Order.Create("buyer-123", "key-123", []);
        order.MarkAsPaid();
        order.MarkAsShipped();

        await updater.Handle(new OrderShippedDomainEvent(order), CancellationToken.None);

        readRepoMock.Verify(x => x.SetOrderAsync(
            It.Is<OrderStatusDto>(d => d.Id == order.Id && d.BuyerId == "buyer-123" && d.Status == "Shipped"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
