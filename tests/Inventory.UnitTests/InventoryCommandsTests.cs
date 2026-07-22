using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    public async Task ReserveStockCommandHandler_Should_Create_Reservation()
    {
        var contextMock = new Mock<IInventoryDbContext>();
        var stock = new Stock("SKU-1", 100);
        contextMock.Setup(x => x.Stocks).ReturnsDbSet(new List<Stock> { stock });
        
        var reservationsList = new List<InventoryReservation>();
        contextMock.Setup(x => x.Reservations).ReturnsDbSet(reservationsList);
        contextMock.Setup(x => x.Reservations.Add(It.IsAny<InventoryReservation>())).Callback<InventoryReservation>(r => reservationsList.Add(r));

        var handler = new ReserveStockCommandHandler(contextMock.Object);
        var result = await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), "SKU-1", 10), CancellationToken.None);

        result.Success.ShouldBeTrue();
        stock.ReservedQuantity.ShouldBe(10);
        contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseStockCommandHandler_Should_Release_Stock()
    {
        var contextMock = new Mock<IInventoryDbContext>();
        var publishMock = new Mock<IPublishEndpoint>();

        var reservation = InventoryReservation.Create(Guid.NewGuid(), "SKU-1", 10);
        contextMock.Setup(x => x.Reservations).ReturnsDbSet(new List<InventoryReservation> { reservation });

        var stock = new Stock("SKU-1", 100);
        stock.Reserve(10);
        contextMock.Setup(x => x.Stocks).ReturnsDbSet(new List<Stock> { stock });

        var handler = new ReleaseStockCommandHandler(contextMock.Object, publishMock.Object);
        var result = await handler.Handle(new ReleaseStockCommand(reservation.Id), CancellationToken.None);

        result.ShouldBeTrue();
        stock.ReservedQuantity.ShouldBe(0);
        contextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publishMock.Verify(x => x.Publish(It.IsAny<ECommerce.Contracts.Events.StockReleasedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStockAvailabilityQueryHandler_Should_Return_Quantity()
    {
        var contextMock = new Mock<IInventoryDbContext>();
        var stock = new Stock("SKU-1", 100);
        contextMock.Setup(x => x.Stocks).ReturnsDbSet(new List<Stock> { stock });

        var handler = new GetStockAvailabilityQueryHandler(contextMock.Object);
        var result = await handler.Handle(new GetStockAvailabilityQuery("SKU-1"), CancellationToken.None);

        result.ShouldBe(100);
    }
}
