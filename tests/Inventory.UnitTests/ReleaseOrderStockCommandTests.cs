using Inventory.Application.Common.Interfaces;
using Inventory.Application.Inventory.Commands;
using Inventory.Domain.Entities;
using MediatR;
using Moq;
using Moq.EntityFrameworkCore;
using Xunit;

namespace Inventory.UnitTests;

public class ReleaseOrderStockCommandTests
{
    [Fact]
    public async Task Handle_ShouldReleaseAllActiveReservations_ForOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        var reservation1 =
            InventoryReservation.Create(orderId, "SKU-1", 1);

        var reservation2 =
            InventoryReservation.Create(orderId, "SKU-2", 2);

        var reservation3 =
            InventoryReservation.Create(orderId, "SKU-3", 3);

        var reservation4 =
            InventoryReservation.Create(orderId, "SKU-4", 4);

        var reservations = new List<InventoryReservation>
        {
            reservation1,
            reservation2,
            reservation3,
            reservation4
        };

        var dbContext = new Mock<IInventoryDbContext>();
        dbContext
            .Setup(x => x.Reservations)
            .ReturnsDbSet(reservations);

        var sender = new Mock<ISender>();

        var handler = new ReleaseOrderStockCommandHandler(
            dbContext.Object,
            sender.Object);

        // Act
        await handler.Handle(
            new ReleaseOrderStockCommand(orderId),
            CancellationToken.None);

        // Assert
        sender.Verify(
            x => x.Send(
                It.IsAny<ReleaseStockCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(4));

        foreach (var reservation in reservations)
        {
            sender.Verify(
                x => x.Send(
                    It.Is<ReleaseStockCommand>(
                        command =>
                            command.ReservationId == reservation.Id),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task Handle_ShouldOnlyReleaseReservations_BelongingToRequestedOrder()
    {
        var orderId = Guid.NewGuid();
        var anotherOrderId = Guid.NewGuid();

        var target1 =
            InventoryReservation.Create(orderId, "SKU-1", 1);

        var target2 =
            InventoryReservation.Create(orderId, "SKU-2", 1);

        var unrelated =
            InventoryReservation.Create(anotherOrderId, "SKU-3", 1);

        var reservations = new List<InventoryReservation>
    {
        target1,
        target2,
        unrelated
    };

        var dbContext = new Mock<IInventoryDbContext>();
        dbContext
            .Setup(x => x.Reservations)
            .ReturnsDbSet(reservations);

        var sender = new Mock<ISender>();

        var handler = new ReleaseOrderStockCommandHandler(
            dbContext.Object,
            sender.Object);

        await handler.Handle(
            new ReleaseOrderStockCommand(orderId),
            CancellationToken.None);

        sender.Verify(
            x => x.Send(
                It.Is<ReleaseStockCommand>(
                    c => c.ReservationId == target1.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);

        sender.Verify(
            x => x.Send(
                It.Is<ReleaseStockCommand>(
                    c => c.ReservationId == target2.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);

        sender.Verify(
            x => x.Send(
                It.Is<ReleaseStockCommand>(
                    c => c.ReservationId == unrelated.Id),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldSkipAlreadyReleasedReservations()
    {
        var orderId = Guid.NewGuid();

        var active =
            InventoryReservation.Create(orderId, "SKU-ACTIVE", 1);

        var released =
            InventoryReservation.Create(orderId, "SKU-RELEASED", 1);

        released.Release();

        var reservations = new List<InventoryReservation>
    {
        active,
        released
    };

        var dbContext = new Mock<IInventoryDbContext>();
        dbContext
            .Setup(x => x.Reservations)
            .ReturnsDbSet(reservations);

        var sender = new Mock<ISender>();

        var handler = new ReleaseOrderStockCommandHandler(
            dbContext.Object,
            sender.Object);

        await handler.Handle(
            new ReleaseOrderStockCommand(orderId),
            CancellationToken.None);

        sender.Verify(
            x => x.Send(
                It.Is<ReleaseStockCommand>(
                    c => c.ReservationId == active.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);

        sender.Verify(
            x => x.Send(
                It.Is<ReleaseStockCommand>(
                    c => c.ReservationId == released.Id),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
