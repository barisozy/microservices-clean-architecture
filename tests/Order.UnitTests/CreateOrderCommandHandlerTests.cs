using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using ECommerce.Contracts.Events.v1;
using ECommerce.Contracts.Protos;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Order.Application.Common.Exceptions;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.Commands.CreateOrder;
using Order.Domain.Exceptions;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public class CreateOrderCommandHandlerTests
{
    private readonly Mock<IOrderWriteRepository> _dbContextMock = new();
    private readonly Mock<IPublishEndpoint> _publishEndpointMock = new();
    private readonly Mock<IOrderCache> _orderCacheMock = new();
    private readonly Mock<IBasketService> _basketServiceMock = new();
    private readonly Mock<CatalogService.CatalogServiceClient> _catalogClientMock = new();
    private readonly Mock<PromotionService.PromotionServiceClient> _promotionClientMock = new();
    private readonly Mock<ILogger<CreateOrderCommandHandler>> _loggerMock = new();

    private CreateOrderCommandHandler CreateHandler()
    {
        return new CreateOrderCommandHandler(
            _dbContextMock.Object,
            _publishEndpointMock.Object,
            _orderCacheMock.Object,
            _basketServiceMock.Object,
            _catalogClientMock.Object,
            _promotionClientMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldPublishCheckoutStartedEvent()
    {
        _orderCacheMock.Setup(x => x.GetOrderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var catalogCall = new AsyncUnaryCall<GetPriceSnapshotResponse>(
            Task.FromResult(new GetPriceSnapshotResponse { Available = true, UnitPrice = new Money { MinorUnits = 10000, Currency = "USD" } }),
            Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        _catalogClientMock.Setup(x => x.GetPriceSnapshotAsync(It.IsAny<GetPriceSnapshotRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(catalogCall);

        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemDto> { new("SKU1", 1, 100) }, null);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);
        _publishEndpointMock.Verify(x => x.Publish(It.IsAny<OrderCheckoutStarted>(), It.IsAny<IPipe<PublishContext<OrderCheckoutStarted>>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_IdempotencyCacheThrows_LogsAndProceeds()
    {
        _orderCacheMock.Setup(x => x.GetOrderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cache failure"));

        var catalogCall = new AsyncUnaryCall<GetPriceSnapshotResponse>(
            Task.FromResult(new GetPriceSnapshotResponse { Available = true, UnitPrice = new Money { MinorUnits = 10000, Currency = "USD" } }),
            Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        _catalogClientMock.Setup(x => x.GetPriceSnapshotAsync(It.IsAny<GetPriceSnapshotRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(catalogCall);

        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemDto> { new("SKU1", 1, 100) }, null);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_IdempotencyLookupSucceeds_ReturnsCachedId()
    {
        var existingId = Guid.NewGuid();
        _orderCacheMock.Setup(x => x.GetOrderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingId);

        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemDto> { new("SKU1", 1, 100) }, null);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.ShouldBe(existingId);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyBasket_ThrowsException()
    {
        _orderCacheMock.Setup(x => x.TryAcquireBasketLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IAsyncDisposable>().Object);
            
        _basketServiceMock.Setup(x => x.GetBasketAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>()); // Empty

        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemDto>(), null);
        await Should.ThrowAsync<OrderDomainException>(() => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CatalogFails_CacheFallbackSucceeds_Proceeds()
    {
        var catalogCall = new AsyncUnaryCall<GetPriceSnapshotResponse>(
            Task.FromException<GetPriceSnapshotResponse>(new RpcException(new Status(StatusCode.Unavailable, "Offline"))),
            Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        _catalogClientMock.Setup(x => x.GetPriceSnapshotAsync(It.IsAny<GetPriceSnapshotRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(catalogCall);

        _orderCacheMock.Setup(x => x.GetCatalogPriceAsync("SKU1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(99m);

        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemDto> { new("SKU1", 1, 100) }, null);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_CatalogFails_CacheFails_ThrowsException()
    {
        var catalogCall = new AsyncUnaryCall<GetPriceSnapshotResponse>(
            Task.FromException<GetPriceSnapshotResponse>(new RpcException(new Status(StatusCode.Unavailable, "Offline"))),
            Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        _catalogClientMock.Setup(x => x.GetPriceSnapshotAsync(It.IsAny<GetPriceSnapshotRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(catalogCall);

        _orderCacheMock.Setup(x => x.GetCatalogPriceAsync("SKU1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cache failure"));

        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemDto> { new("SKU1", 1, 100) }, null);
        await Should.ThrowAsync<OrderDomainException>(() => CreateHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CouponApplicationThrows_ThrowsOrderDomainException()
    {
        var catalogCall = new AsyncUnaryCall<GetPriceSnapshotResponse>(
            Task.FromResult(new GetPriceSnapshotResponse { Available = true, UnitPrice = new Money { MinorUnits = 10000, Currency = "USD" } }),
            Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        _catalogClientMock.Setup(x => x.GetPriceSnapshotAsync(It.IsAny<GetPriceSnapshotRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(catalogCall);

        var promoCall = new AsyncUnaryCall<ApplyCouponResponse>(
            Task.FromException<ApplyCouponResponse>(new RpcException(new Status(StatusCode.Unavailable, "Offline"))),
            Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => new Metadata(), () => { });
        _promotionClientMock.Setup(x => x.ApplyCouponAsync(It.IsAny<ApplyCouponRequest>(), null, null, It.IsAny<CancellationToken>()))
            .Returns(promoCall);

        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemDto> { new("SKU1", 1, 100) }, "DISCOUNT10");
        
        await Should.ThrowAsync<OrderDomainException>(() => CreateHandler().Handle(command, CancellationToken.None));
    }
}
