using Inventory.Domain.Entities;
using Shouldly;
using Xunit;

namespace Inventory.UnitTests;

public sealed class InventoryReservationLeaseTests
{
    [Fact]
    public void Pending_can_be_committed_before_expiry()
    {
        var now = DateTimeOffset.UtcNow;
        var reservation = InventoryReservation.Create(Guid.CreateVersion7(), "SKU-1", 2, now.AddMinutes(2));

        reservation.Commit(now).ShouldBeTrue();
        reservation.Status.ShouldBe(InventoryReservationStatus.Committed);
        reservation.Expire(now.AddMinutes(3)).ShouldBeFalse();
    }

    [Fact]
    public void Pending_can_expire_but_expired_cannot_be_committed()
    {
        var now = DateTimeOffset.UtcNow;
        var reservation = InventoryReservation.Create(Guid.CreateVersion7(), "SKU-1", 2, now.AddSeconds(1));

        reservation.Expire(now.AddSeconds(2)).ShouldBeTrue();
        reservation.Status.ShouldBe(InventoryReservationStatus.Expired);
        reservation.Commit(now.AddSeconds(2)).ShouldBeFalse();
    }

    [Fact]
    public void Release_is_idempotent_and_does_not_reopen_reservation()
    {
        var reservation = InventoryReservation.Create(Guid.CreateVersion7(), "SKU-1", 2, DateTimeOffset.UtcNow.AddMinutes(2));

        reservation.Release(DateTimeOffset.UtcNow).ShouldBeTrue();
        reservation.Release(DateTimeOffset.UtcNow).ShouldBeFalse();
        reservation.Status.ShouldBe(InventoryReservationStatus.Released);
        reservation.Commit(DateTimeOffset.UtcNow).ShouldBeFalse();
    }

    [Fact]
    public void Committed_can_be_released_but_cannot_expire()
    {
        var now = DateTimeOffset.UtcNow;
        var reservation = InventoryReservation.Create(Guid.CreateVersion7(), "SKU-1", 2, now.AddMinutes(2));

        reservation.Commit(now).ShouldBeTrue();
        reservation.Release(now.AddSeconds(1)).ShouldBeTrue();

        reservation.Status.ShouldBe(InventoryReservationStatus.Released);
        reservation.Expire(now.AddMinutes(3)).ShouldBeFalse();
    }

    [Fact]
    public void Duplicate_commit_is_idempotent()
    {
        var now = DateTimeOffset.UtcNow;
        var reservation = InventoryReservation.Create(Guid.CreateVersion7(), "SKU-1", 2, now.AddMinutes(2));

        reservation.Commit(now).ShouldBeTrue();
        reservation.Commit(now.AddSeconds(1)).ShouldBeTrue();
        reservation.Status.ShouldBe(InventoryReservationStatus.Committed);
    }
}
