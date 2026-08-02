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
    public async Task Handle_ShouldPublishOrderCreatedEvent()
    {
        var dbContextMock = new Mock<IOrderDbContext>();
        var dbSetMock = new Mock<DbSet<global::Order.Domain.Entities.Order>>();
        dbContextMock.Setup(x => x.Orders).Returns(dbSetMock.Object);

        var publishEndpointMock = new Mock<IPublishEndpoint>();

        var orderCacheMock = new Mock<IOrderCache>();
        orderCacheMock.Setup(x => x.GetOrderIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var basketServiceMock = new Mock<IBasketService>();
        var inventoryClientMock = new Mock<InventoryService.InventoryServiceClient>();
        var catalogClientMock = new Mock<CatalogService.CatalogServiceClient>();
        var promotionClientMock = new Mock<PromotionService.PromotionServiceClient>();
        var loggerMock = new Mock<ILogger<CreateOrderCommandHandler>>();

        var reserveCall = new AsyncUnaryCall<ReserveStockResponse>(
            Task.FromResult(new ReserveStockResponse { IsSuccess = true, ReservationId = Guid.NewGuid().ToString(), Message = "Success" }),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

        inventoryClientMock.Setup(x => x.ReserveStockAsync(It.IsAny<ReserveStockRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(reserveCall);

        var handler = new CreateOrderCommandHandler(
            dbContextMock.Object,
            publishEndpointMock.Object,
            orderCacheMock.Object,
            basketServiceMock.Object,
            inventoryClientMock.Object,
            catalogClientMock.Object,
            promotionClientMock.Object,
            loggerMock.Object);

        var command = new CreateOrderCommand(Guid.NewGuid(), Guid.NewGuid(), "key1", new List<OrderItemDto> { new("SKU1", 1, 100) });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);

        publishEndpointMock.Verify(x => x.Publish(It.IsAny<OrderCreated>(), It.IsAny<CancellationToken>()), Times.Once);
        dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
