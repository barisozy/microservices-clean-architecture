using Inventory.Domain.Common;
using Inventory.Domain.Events;
using Inventory.Domain.Entities;
using System;
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
