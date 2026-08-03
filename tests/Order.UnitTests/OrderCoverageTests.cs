using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.EntityFrameworkCore;
using Order.Application.Basket.Commands;
using Order.Application.Common.Interfaces;
using Order.Application.Consumers;
using Order.Application.Orders.EventHandlers;
using Order.Application.Orders.Queries;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Order.Domain.Events;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public class OrderCoverageTests
{
    [Fact]
    public async Task GetBasketHandler_DelegatesAndReturnsBasket()
    {
        var basket = new Dictionary<string, int> { ["SKU-1"] = 2 };
        var service = new Mock<IBasketService>();
        service.Setup(x => x.GetBasketAsync("buyer", It.IsAny<CancellationToken>())).ReturnsAsync(basket);

        var result = await new GetBasketQueryHandler(service.Object).Handle(new GetBasketQuery("buyer"), CancellationToken.None);

        result.ShouldBe(basket);
    }

    [Fact]
    public async Task ReadModelUpdater_StoresCreatedAndCancelledStatuses()
    {
        var repository = new Mock<IOrderReadRepository>();
        var order = Order.Domain.Entities.Order.Create("buyer", "key", []);
        var updater = new OrderReadModelUpdater(repository.Object);

        await updater.Handle(new OrderCreatedDomainEvent(order), CancellationToken.None);
        order.Cancel("payment failed");
        await updater.Handle(new OrderCancelledDomainEvent(order, "payment failed"), CancellationToken.None);

        repository.Verify(x => x.SetOrderAsync(
            It.Is<OrderStatusDto>(dto => dto.Id == order.Id && dto.Status == "Pending" && dto.BuyerId == "buyer"),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SetOrderAsync(
            It.Is<OrderStatusDto>(dto => dto.Id == order.Id && dto.Status == "Cancelled"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PaymentFailedConsumer_CancelsExistingOrderAndPublishesCompensation()
    {
        var order = Order.Domain.Entities.Order.Create("buyer", "key", []);
        var db = new Mock<IOrderDbContext>();
        db.Setup(x => x.Orders).ReturnsDbSet([order]);
        var publisher = new Mock<IPublishEndpoint>();
        var consumer = new PaymentFailedConsumer(db.Object, publisher.Object, Mock.Of<ILogger<PaymentFailedConsumer>>());
        var message = new PaymentFailed(order.Id, "key", "declined", DateTimeOffset.UtcNow);
        var context = new Mock<ConsumeContext<PaymentFailed>>();
        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(context.Object);

        order.Status.ShouldBe(OrderStatus.Cancelled);
        db.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(x => x.Publish(It.Is<OrderCancelled>(e => e.OrderId == order.Id && e.Reason == "declined"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PaymentFailedConsumer_DoesNothingWhenOrderDoesNotExist()
    {
        var db = new Mock<IOrderDbContext>();
        db.Setup(x => x.Orders).ReturnsDbSet(new List<Order.Domain.Entities.Order>());
        var publisher = new Mock<IPublishEndpoint>();
        var consumer = new PaymentFailedConsumer(db.Object, publisher.Object, Mock.Of<ILogger<PaymentFailedConsumer>>());
        var context = new Mock<ConsumeContext<PaymentFailed>>();
        context.SetupGet(x => x.Message).Returns(new PaymentFailed(Guid.NewGuid(), "key", "declined", DateTimeOffset.UtcNow));
        context.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(context.Object);

        db.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        publisher.Verify(x => x.Publish(It.IsAny<OrderCancelled>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
