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
using Inventory.Application.Common.Exceptions;

namespace Inventory.UnitTests;

public class InventoryCommandsTests
{
    [Fact]
    public async Task ReserveStock_ShouldReturnSuccess_WhenStockIsAvailable()
    {
        var dbContextMock = new Mock<IInventoryWriteRepository>();
        
        dbContextMock.Setup(x => x.FindStockAsync("SKU1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Stock("SKU1", 100));
        
        dbContextMock.Setup(x => x.FindReservationByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryReservation?)null);

        var readRepoMock = new Mock<IStockReadRepository>();

        var handler = new ReserveStockCommandHandler(dbContextMock.Object, readRepoMock.Object);

        var result = await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), "SKU1", 10), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEqual(Guid.Empty, result.ReservationId);
    }

    [Fact]
    public async Task ReleaseStock_ShouldPublishStockReleasedEvent()
    {
        var dbContextMock = new Mock<IInventoryWriteRepository>();
        var publishEndpointMock = new Mock<IPublishEndpoint>();

        var reservation = InventoryReservation.Create(Guid.NewGuid(), "SKU1", 10, DateTimeOffset.UtcNow.AddMinutes(2));
        dbContextMock.Setup(x => x.FindReservationAsync(reservation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        dbContextMock.Setup(x => x.FindStockAsync("SKU1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Stock("SKU1", 90));

        var readRepoMock = new Mock<IStockReadRepository>();

        var handler = new ReleaseStockCommandHandler(dbContextMock.Object, publishEndpointMock.Object, readRepoMock.Object);

        var result = await handler.Handle(new ReleaseStockCommand(reservation.Id), CancellationToken.None);

        Assert.True(result);
        publishEndpointMock.Verify(x => x.Publish(It.IsAny<StockReleased>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStockAvailabilityQueryHandler_Should_Return_Quantity()
    {
        var readRepoMock = new Mock<IStockReadRepository>();
        readRepoMock.Setup(x => x.GetAvailableQuantityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var handler = new GetStockAvailabilityQueryHandler(readRepoMock.Object);
        var result = await handler.Handle(new GetStockAvailabilityQuery("SKU-1"), CancellationToken.None);

        result.ShouldBe(0);
    }

    [Fact]
    public async Task GetStockAvailabilityQueryHandler_Should_Return_Cached_Quantity()
    {
        var readRepoMock = new Mock<IStockReadRepository>();
        readRepoMock.Setup(x => x.GetAvailableQuantityAsync("SKU-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(55);

        var handler = new GetStockAvailabilityQueryHandler(readRepoMock.Object);
        var result = await handler.Handle(new GetStockAvailabilityQuery("SKU-1"), CancellationToken.None);

        result.ShouldBe(55);
    }

    [Fact]
    public async Task ReserveStock_ShouldReturnFalse_WhenStockIsInsufficient()
    {
        var dbContextMock = new Mock<IInventoryWriteRepository>();
        dbContextMock.Setup(x => x.FindStockAsync("SKU1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Stock("SKU1", 5));
        dbContextMock.Setup(x => x.FindReservationByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryReservation?)null);
        
        var readRepoMock = new Mock<IStockReadRepository>();

        var handler = new ReserveStockCommandHandler(dbContextMock.Object, readRepoMock.Object);

        var result = await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), "SKU1", 10), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Message.ShouldBe("Insufficient stock availability.");
    }

    [Fact]
    public async Task ReserveStock_ShouldRejectUnknownSku_IfNotProvisioned()
    {
        var dbContextMock = new Mock<IInventoryWriteRepository>();
        dbContextMock.Setup(x => x.FindStockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stock?)null);
        dbContextMock.Setup(x => x.FindReservationByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryReservation?)null);
        var readRepoMock = new Mock<IStockReadRepository>();

        var handler = new ReserveStockCommandHandler(dbContextMock.Object, readRepoMock.Object);

        var result = await handler.Handle(new ReserveStockCommand(Guid.NewGuid(), "SKU_NEW", 10), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.Message.ShouldBe("Unknown SKU. Inventory must be provisioned before checkout.");
        dbContextMock.Verify(x => x.Add(It.IsAny<Stock>()), Times.Never);
    }

    [Fact]
    public async Task ReserveStock_ShouldFailSafe_WhenOptimisticConcurrencyDetectsAConflict()
    {
        var dbContextMock = new Mock<IInventoryWriteRepository>();
        dbContextMock.Setup(x => x.FindStockAsync("SKU1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Stock("SKU1", 100));
        dbContextMock.Setup(x => x.FindReservationByOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryReservation?)null);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PersistenceConcurrencyException(new Exception()));
        var readRepoMock = new Mock<IStockReadRepository>();

        var result = await new ReserveStockCommandHandler(dbContextMock.Object, readRepoMock.Object)
            .Handle(new ReserveStockCommand(Guid.NewGuid(), "SKU1", 1), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ReservationId.ShouldBe(Guid.Empty);
        result.Message.ShouldContain("concurrently");
        readRepoMock.Verify(
            repository => repository.SetAvailableQuantityAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReleaseStock_ShouldReturnFalse_IfReservationNotFound()
    {
        var dbContextMock = new Mock<IInventoryWriteRepository>();
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        dbContextMock.Setup(x => x.FindReservationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryReservation?)null);
        var readRepoMock = new Mock<IStockReadRepository>();

        var handler = new ReleaseStockCommandHandler(dbContextMock.Object, publishEndpointMock.Object, readRepoMock.Object);

        var result = await handler.Handle(new ReleaseStockCommand(Guid.NewGuid()), CancellationToken.None);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ReleaseStock_ShouldBeIdempotent_WhenReservationWasAlreadyReleased()
    {
        var dbContextMock = new Mock<IInventoryWriteRepository>();
        var reservation = InventoryReservation.Create(Guid.NewGuid(), "SKU-1", 2, DateTimeOffset.UtcNow.AddMinutes(2));
        reservation.Release(DateTimeOffset.UtcNow);
        dbContextMock.Setup(x => x.FindReservationAsync(reservation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var readRepoMock = new Mock<IStockReadRepository>();

        var result = await new ReleaseStockCommandHandler(dbContextMock.Object, publishEndpointMock.Object, readRepoMock.Object)
            .Handle(new ReleaseStockCommand(reservation.Id), CancellationToken.None);

        result.ShouldBeTrue();
        publishEndpointMock.Verify(x => x.Publish(It.IsAny<StockReleased>(), It.IsAny<CancellationToken>()), Times.Never);
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
