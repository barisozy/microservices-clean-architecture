using System;
using System.Collections.Generic;
using Inventory.Domain.Entities;
using Shouldly;
using Xunit;

namespace Inventory.UnitTests;

public class InventoryReservationTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateReservation()
    {
        var orderId = Guid.NewGuid();
        var items = new Dictionary<string, int> { { "SKU1", 5 } };
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        var reservation = InventoryReservation.Create(orderId, items, expiresAt);

        reservation.OrderId.ShouldBe(orderId);
        reservation.Status.ShouldBe(InventoryReservationStatus.Pending);
        reservation.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Create_WithEmptyOrderId_ShouldThrowException()
    {
        Should.Throw<ArgumentException>(() =>
            InventoryReservation.Create(Guid.Empty, new Dictionary<string, int> { { "SKU1", 5 } }, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_WithEmptyItems_ShouldThrowException()
    {
        Should.Throw<ArgumentException>(() =>
            InventoryReservation.Create(Guid.NewGuid(), new Dictionary<string, int>(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Release_WhenPending_ShouldChangeStatus()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), new Dictionary<string, int> { { "SKU1", 5 } }, DateTimeOffset.UtcNow.AddMinutes(15));
        reservation.Release(DateTimeOffset.UtcNow);
        reservation.Status.ShouldBe(InventoryReservationStatus.Released);
    }

    [Fact]
    public void Release_WhenAlreadyReleased_ShouldBeIdempotent()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), new Dictionary<string, int> { { "SKU1", 5 } }, DateTimeOffset.UtcNow.AddMinutes(15));
        reservation.Release(DateTimeOffset.UtcNow);
        reservation.Release(DateTimeOffset.UtcNow); // idempotent
        reservation.Status.ShouldBe(InventoryReservationStatus.Released);
    }
}
