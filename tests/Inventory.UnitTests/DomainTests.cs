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
    public void Stock_PropertiesAndMethods_ShouldWorkCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var stock = new Stock("SKU-100", 50)
        {
            CreatedAt = now,
            CreatedBy = "admin",
            LastModifiedAt = now,
            LastModifiedBy = "admin2"
        };

        stock.Sku.ShouldBe("SKU-100");
        stock.Quantity.ShouldBe(50);
        stock.ReservedQuantity.ShouldBe(0);
        stock.AvailableQuantity.ShouldBe(50);
        stock.CreatedAt.ShouldBe(now);
        stock.CreatedBy.ShouldBe("admin");

        stock.Reserve(10).ShouldBeTrue();
        stock.ReservedQuantity.ShouldBe(10);
        stock.AvailableQuantity.ShouldBe(40);

        stock.Release(5);
        stock.ReservedQuantity.ShouldBe(5);
        stock.AvailableQuantity.ShouldBe(45);
    }

    [Fact]
    public void InventoryReservation_PropertiesAndMethods_ShouldWorkCorrectly()
    {
        var orderId = Guid.NewGuid();
        var reservation = InventoryReservation.Create(orderId, "SKU-200", 3);

        reservation.OrderId.ShouldBe(orderId);
        reservation.Sku.ShouldBe("SKU-200");
        reservation.Quantity.ShouldBe(3);
        reservation.IsReleased.ShouldBeFalse();

        reservation.Release();
        reservation.IsReleased.ShouldBeTrue();
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
