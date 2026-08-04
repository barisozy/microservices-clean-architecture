using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using ECommerce.Contracts.Events.v1;
using ECommerce.Contracts.Protos;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.Commands.CreateOrder;
using Order.Domain.Entities;
using Xunit;
using Grpc.Core;

namespace Order.UnitTests;

public class CreateOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldPublishCheckoutStartedEvent()
    {
        var dbContextMock = new Mock<IOrderDbContext>();
        var dbSetMock = new Mock<DbSet<global::Order.Domain.Entities.Order>>();
        dbContextMock.Setup(x => x.Orders).Returns(dbSetMock.Object);

        var publishEndpointMock = new Mock<IPublishEndpoint>();

        var orderCacheMock = new Mock<IOrderCache>();
        orderCacheMock.Setup(x => x.GetOrderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var basketServiceMock = new Mock<IBasketService>();
        var catalogClientMock = new Mock<CatalogService.CatalogServiceClient>();
        var promotionClientMock = new Mock<PromotionService.PromotionServiceClient>();
        var loggerMock = new Mock<ILogger<CreateOrderCommandHandler>>();

        var catalogCall = new AsyncUnaryCall<GetPriceSnapshotResponse>(
            Task.FromResult(new GetPriceSnapshotResponse { Available = true, UnitPrice = 100 }),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
        catalogClientMock.Setup(x => x.GetPriceSnapshotAsync(It.IsAny<GetPriceSnapshotRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(catalogCall);

        var handler = new CreateOrderCommandHandler(
            dbContextMock.Object,
            publishEndpointMock.Object,
            orderCacheMock.Object,
            basketServiceMock.Object,
            catalogClientMock.Object,
            promotionClientMock.Object,
            loggerMock.Object);

        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemDto> { new("SKU1", 1, 100) });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);

        publishEndpointMock.Verify(x => x.Publish<CheckoutStarted>(
            It.IsAny<CheckoutStarted>(),
            It.IsAny<IPipe<PublishContext<CheckoutStarted>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
