using Inventory.Application.Common.Interfaces;
using Inventory.Application.Inventory.Commands;
using Inventory.Domain.Entities;
using MediatR;
using Moq;
using Xunit;

namespace Inventory.UnitTests;

public class ReleaseOrderStockCommandTests
{
    [Fact]
    public async Task Handle_ShouldReleaseActiveReservation_ForOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        var reservation1 =
            InventoryReservation.Create(orderId, "SKU-1", 1, DateTimeOffset.UtcNow.AddMinutes(2));

        var dbContext = new Mock<IInventoryWriteRepository>();
        dbContext
            .Setup(x => x.FindReservationByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation1);

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
            Times.Once);

        sender.Verify(
            x => x.Send(
                It.Is<ReleaseStockCommand>(
                    command =>
                        command.ReservationId == reservation1.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldOnlyReleaseReservation_BelongingToRequestedOrder()
    {
        var orderId = Guid.NewGuid();
        var anotherOrderId = Guid.NewGuid();

        var target1 =
            InventoryReservation.Create(orderId, "SKU-1", 1, DateTimeOffset.UtcNow.AddMinutes(2));

        var dbContext = new Mock<IInventoryWriteRepository>();
        dbContext
            .Setup(x => x.FindReservationByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target1);
        dbContext
            .Setup(x => x.FindReservationByOrderIdAsync(anotherOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryReservation?)null);

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
    }

    [Fact]
    public async Task Handle_ShouldSkipAlreadyReleasedReservations()
    {
        var orderId = Guid.NewGuid();

        var released =
            InventoryReservation.Create(orderId, "SKU-RELEASED", 1, DateTimeOffset.UtcNow.AddMinutes(2));

        released.Release(DateTimeOffset.UtcNow);

        var dbContext = new Mock<IInventoryWriteRepository>();
        dbContext
            .Setup(x => x.FindReservationByOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(released);

        var sender = new Mock<ISender>();

        var handler = new ReleaseOrderStockCommandHandler(
            dbContext.Object,
            sender.Object);

        await handler.Handle(
            new ReleaseOrderStockCommand(orderId),
            CancellationToken.None);

        sender.Verify(
            x => x.Send(
                It.IsAny<ReleaseStockCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
