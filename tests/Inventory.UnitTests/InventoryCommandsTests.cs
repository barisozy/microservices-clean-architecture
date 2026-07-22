using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Contracts.Events.v1;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Inventory.Commands;
using Inventory.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Inventory.UnitTests;

public class InventoryCommandsTests
{
    [Fact]
    public async Task ReserveStock_ShouldReturnSuccess_WhenStockIsAvailable()
    {
        var dbContextMock = new Mock<IInventoryDbContext>();
        
        var stocks = new List<Stock> { new Stock("SKU1", 100) };
        dbContextMock.Setup(x => x.Stocks).ReturnsDbSet(stocks);
        
        var reservations = new List<InventoryReservation>();
        dbContextMock.Setup(x => x.Reservations).ReturnsDbSet(reservations);

        var readRepoMock = new Mock<IStockReadRepository>();

        var handler = new ReserveStockCommandHandler(dbContextMock.Object, readRepoMock.Object);

        var result = await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), "SKU1", 10), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.ReservationId);
    }

    [Fact]
    public async Task ReleaseStock_ShouldPublishStockReleasedEvent()
    {
        var dbContextMock = new Mock<IInventoryDbContext>();
        var publishEndpointMock = new Mock<IPublishEndpoint>();

        var reservation = InventoryReservation.Create(Guid.NewGuid(), "SKU1", 10);
        var reservations = new List<InventoryReservation> { reservation };
        dbContextMock.Setup(x => x.Reservations).ReturnsDbSet(reservations);

        var stocks = new List<Stock> { new Stock("SKU1", 90) };
        dbContextMock.Setup(x => x.Stocks).ReturnsDbSet(stocks);

        var readRepoMock = new Mock<IStockReadRepository>();

        var handler = new ReleaseStockCommandHandler(dbContextMock.Object, publishEndpointMock.Object, readRepoMock.Object);

        var result = await handler.Handle(new ReleaseStockCommand(reservation.Id), CancellationToken.None);

        Assert.True(result);
        publishEndpointMock.Verify(x => x.Publish(It.IsAny<StockReleased>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStockAvailabilityQueryHandler_Should_Return_Quantity()
    {
        var contextMock = new Mock<IInventoryDbContext>();
        var stock = new Stock("SKU-1", 100);
        contextMock.Setup(x => x.Stocks).ReturnsDbSet(new List<Stock> { stock });

        var readRepoMock = new Mock<IStockReadRepository>();
        readRepoMock.Setup(x => x.GetAvailableQuantityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null); // Simulate cache miss

        var handler = new GetStockAvailabilityQueryHandler(contextMock.Object, readRepoMock.Object);
        var result = await handler.Handle(new GetStockAvailabilityQuery("SKU-1"), CancellationToken.None);

        result.ShouldBe(100);
    }

    [Fact]
    public async Task GetStockAvailabilityQueryHandler_Should_Return_Cached_Quantity()
    {
        var contextMock = new Mock<IInventoryDbContext>();
        var readRepoMock = new Mock<IStockReadRepository>();
        readRepoMock.Setup(x => x.GetAvailableQuantityAsync("SKU-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(55);

        var handler = new GetStockAvailabilityQueryHandler(contextMock.Object, readRepoMock.Object);
        var result = await handler.Handle(new GetStockAvailabilityQuery("SKU-1"), CancellationToken.None);

        result.ShouldBe(55);
        contextMock.Verify(x => x.Stocks, Times.Never);
    }

    [Fact]
    public async Task ReserveStock_ShouldReturnFalse_WhenStockIsInsufficient()
    {
        var dbContextMock = new Mock<IInventoryDbContext>();
        var stocks = new List<Stock> { new Stock("SKU1", 5) };
        dbContextMock.Setup(x => x.Stocks).ReturnsDbSet(stocks);
        
        var readRepoMock = new Mock<IStockReadRepository>();

        var handler = new ReserveStockCommandHandler(dbContextMock.Object, readRepoMock.Object);

        var result = await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), "SKU1", 10), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Message.ShouldBe("Insufficient stock availability.");
    }

    [Fact]
    public async Task ReserveStock_ShouldCreateNewStock_IfNotFound()
    {
        var dbContextMock = new Mock<IInventoryDbContext>();
        var stocks = new List<Stock>();
        dbContextMock.Setup(x => x.Stocks).ReturnsDbSet(stocks);
        var reservations = new List<InventoryReservation>();
        dbContextMock.Setup(x => x.Reservations).ReturnsDbSet(reservations);
        var readRepoMock = new Mock<IStockReadRepository>();

        var handler = new ReserveStockCommandHandler(dbContextMock.Object, readRepoMock.Object);

        var result = await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), "SKU_NEW", 10), CancellationToken.None);

        result.Success.ShouldBeTrue();
        dbContextMock.Verify(x => x.Stocks.Add(It.IsAny<Stock>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseStock_ShouldReturnFalse_IfReservationNotFound()
    {
        var dbContextMock = new Mock<IInventoryDbContext>();
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var reservations = new List<InventoryReservation>();
        dbContextMock.Setup(x => x.Reservations).ReturnsDbSet(reservations);
        var readRepoMock = new Mock<IStockReadRepository>();

        var handler = new ReleaseStockCommandHandler(dbContextMock.Object, publishEndpointMock.Object, readRepoMock.Object);

        var result = await handler.Handle(new ReleaseStockCommand(Guid.NewGuid()), CancellationToken.None);

        result.ShouldBeFalse();
    }
}
