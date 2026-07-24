using System;
using Inventory.Domain.Common;
using Inventory.Domain.Entities;
using Inventory.Domain.Events;
using Shouldly;
using Xunit;

namespace Inventory.UnitTests;

public class DomainTests
{
    private class TestEntity : BaseEntity { }

    [Fact]
    public void BaseEntity_Should_Manage_Events()
    {
        var entity = new TestEntity();
        var reservation = InventoryReservation.Create(Guid.NewGuid(), "sku", 10);
        var domainEvent = new StockReservedDomainEvent(reservation);

        entity.AddDomainEvent(domainEvent);
        entity.DomainEvents.ShouldContain(domainEvent);

        entity.RemoveDomainEvent(domainEvent);
        entity.DomainEvents.ShouldNotContain(domainEvent);

        entity.AddDomainEvent(domainEvent);
        entity.ClearDomainEvents();
        entity.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void BaseAuditableEntity_Properties_ShouldBeSetAndGet()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new InventoryItem
        {
            Sku = "SKU-100",
            Quantity = 50,
            ReservedQuantity = 5,
            CreatedAt = now,
            CreatedBy = "admin",
            LastModifiedAt = now,
            LastModifiedBy = "admin2"
        };

        item.Sku.ShouldBe("SKU-100");
        item.Quantity.ShouldBe(50);
        item.ReservedQuantity.ShouldBe(5);
        item.CreatedAt.ShouldBe(now);
        item.CreatedBy.ShouldBe("admin");
        item.LastModifiedAt.ShouldBe(now);
        item.LastModifiedBy.ShouldBe("admin2");
    }

    [Fact]
    public void InventoryReservation_Properties_ShouldBeSetAndGet()
    {
        var orderId = Guid.NewGuid();
        var reservation = InventoryReservation.Create(orderId, "SKU-200", 3);
        reservation.Status = ReservationStatus.Released;

        reservation.OrderId.ShouldBe(orderId);
        reservation.Sku.ShouldBe("SKU-200");
        reservation.Quantity.ShouldBe(3);
        reservation.Status.ShouldBe(ReservationStatus.Released);
    }

    [Fact]
    public void StockReservedDomainEvent_Should_Store_Values()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), "sku", 10);
        var evt = new StockReservedDomainEvent(reservation);
        evt.Reservation.ShouldBe(reservation);
    }

    [Fact]
    public void StockReleasedDomainEvent_Should_Store_Values()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), "sku", 10);
        var evt = new StockReleasedDomainEvent(reservation);
        evt.Reservation.ShouldBe(reservation);
    }
}
