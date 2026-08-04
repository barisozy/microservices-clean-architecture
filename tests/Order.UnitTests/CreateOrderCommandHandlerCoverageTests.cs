using ECommerce.Contracts.Events.v1;
using ECommerce.Contracts.Protos;
using Grpc.Core;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.Commands.CreateOrder;
using Order.Domain.Exceptions;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

/// <summary>
/// Exercises checkout policy branches without a broker, Valkey server, or gRPC server.
/// The generated clients are mocked at their public boundary so the tests remain stable.
/// </summary>
public class CreateOrderCommandHandlerCoverageTests
{
    [Fact]
    public async Task Handle_ReturnsCachedOrder_WithoutCallingAnyRemoteService()
    {
        var fixture = new HandlerFixture();
        var expected = Guid.NewGuid();
        fixture.OrderCache.Setup(x => x.GetOrderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await fixture.Handler.Handle(fixture.Command(), CancellationToken.None);

        result.ShouldBe(expected);
        fixture.Context.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UsesCatalogSnapshotAndValidCoupon_WhenBothAreAvailable()
    {
        var fixture = new HandlerFixture();
        fixture.Catalog.Setup(x => x.GetPriceSnapshotAsync(
                It.IsAny<GetPriceSnapshotRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(Call(new GetPriceSnapshotResponse { Available = true, UnitPrice = 25 }));
        fixture.Promotion.Setup(x => x.ApplyCouponAsync(
                It.IsAny<ApplyCouponRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(Call(new ApplyCouponResponse { IsValid = true, DiscountedTotal = 40 }));

        await fixture.Handler.Handle(fixture.Command(items: [new("SKU-1", 2, 10)], couponCode: "SAVE"), CancellationToken.None);

        fixture.Publisher.Verify(x => x.Publish<OrderCheckoutStarted>(
            It.Is<OrderCheckoutStarted>(e => e.TotalAmount == 40m && e.Items.Single().UnitPrice == 25m),
            It.IsAny<IPipe<PublishContext<OrderCheckoutStarted>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UsesCachedPrice_WhenCatalogIsUnavailable()
    {
        var fixture = new HandlerFixture();
        fixture.Catalog.Setup(x => x.GetPriceSnapshotAsync(
                It.IsAny<GetPriceSnapshotRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Unavailable, "catalog unavailable")));
        fixture.OrderCache.Setup(x => x.GetCatalogPriceAsync("SKU-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(15m);

        await fixture.Handler.Handle(fixture.Command(items: [new("SKU-1", 2, 12)]), CancellationToken.None);

        fixture.Publisher.Verify(x => x.Publish<OrderCheckoutStarted>(
            It.Is<OrderCheckoutStarted>(e => e.TotalAmount == 30m && e.Items.Single().UnitPrice == 15m),
            It.IsAny<IPipe<PublishContext<OrderCheckoutStarted>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PersistsCheckoutIntent_WithoutCallingInventory()
    {
        var fixture = new HandlerFixture();

        (await fixture.Handler.Handle(fixture.Command(), CancellationToken.None)).ShouldNotBe(Guid.Empty);
        fixture.Publisher.Verify(x => x.Publish<OrderCheckoutStarted>(
            It.IsAny<OrderCheckoutStarted>(),
            It.IsAny<IPipe<PublishContext<OrderCheckoutStarted>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Context.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BasketCheckout_Throws_WhenLockNotAcquired()
    {
        var fixture = new HandlerFixture();
        fixture.SetupLockAcquired(false);

        var ex = await Should.ThrowAsync<Order.Application.Common.Exceptions.BasketUnavailableException>(
            () => fixture.Handler.Handle(fixture.Command(items: []), CancellationToken.None));
        ex.Message.ShouldContain("already in progress");
    }

    [Fact]
    public async Task Handle_BasketCheckout_Throws_WhenBasketIsEmpty()
    {
        var fixture = new HandlerFixture();
        fixture.BasketService.Setup(x => x.GetBasketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());

        var ex = await Should.ThrowAsync<OrderDomainException>(() => fixture.Handler.Handle(fixture.Command(items: []), CancellationToken.None));
        ex.Message.ShouldContain("Basket is empty");
    }

    [Fact]
    public async Task Handle_BasketCheckout_SucceedsAndClearsBasket()
    {
        var fixture = new HandlerFixture();
        fixture.BasketService.Setup(x => x.GetBasketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int> { ["SKU-BASKET"] = 3 });

        var orderId = await fixture.Handler.Handle(fixture.Command(items: []), CancellationToken.None);

        orderId.ShouldNotBe(Guid.Empty);
        fixture.BasketService.Verify(x => x.DeleteBasketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ContinuesWithOriginalTotal_WhenPromotionThrows()
    {
        var fixture = new HandlerFixture();
        fixture.Promotion.Setup(x => x.ApplyCouponAsync(
                It.IsAny<ApplyCouponRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.Internal, "promotion service error")));

        await fixture.Handler.Handle(fixture.Command(items: [new("SKU-1", 1, 50)], couponCode: "BROKEN"), CancellationToken.None);

        fixture.Publisher.Verify(x => x.Publish<OrderCheckoutStarted>(
            It.Is<OrderCheckoutStarted>(e => e.TotalAmount == 50m),
            It.IsAny<IPipe<PublishContext<OrderCheckoutStarted>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AsyncUnaryCall<T> Call<T>(T response) where T : class => new(
        Task.FromResult(response), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });

    private sealed class HandlerFixture
    {
        public Mock<IOrderDbContext> Context { get; } = new();
        public Mock<IPublishEndpoint> Publisher { get; } = new();
        public Mock<IOrderCache> OrderCache { get; } = new();
        public Mock<IAsyncDisposable> BasketLock { get; } = new();
        public Mock<IBasketService> BasketService { get; } = new();
        public Mock<CatalogService.CatalogServiceClient> Catalog { get; } = new();
        public Mock<PromotionService.PromotionServiceClient> Promotion { get; } = new();
        public CreateOrderCommandHandler Handler { get; }

        public HandlerFixture()
        {
            Context.Setup(x => x.Orders).Returns(new Mock<DbSet<Order.Domain.Entities.Order>>().Object);
            Context.Setup(x => x.FindByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Order.Domain.Entities.Order?)null);
            OrderCache.Setup(x => x.GetOrderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid?)null);

            SetupLockAcquired(true);

            Catalog.Setup(x => x.GetPriceSnapshotAsync(
                    It.IsAny<GetPriceSnapshotRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .Returns(Call(new GetPriceSnapshotResponse { Available = true, UnitPrice = 50 }));

            Handler = new CreateOrderCommandHandler(
                Context.Object, Publisher.Object, OrderCache.Object, BasketService.Object,
                Catalog.Object, Promotion.Object, Mock.Of<ILogger<CreateOrderCommandHandler>>());
        }

        public void SetupLockAcquired(bool acquired)
        {
            OrderCache.Setup(x => x.TryAcquireBasketLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(acquired ? BasketLock.Object : null);
        }

        public CreateOrderCommand Command(List<OrderItemDto>? items = null, string? couponCode = null) =>
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString(), items ?? [new("SKU-1", 1, 10)], couponCode);
    }
}
