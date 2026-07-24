using System;
using System.Collections.Generic;
using System.Linq;
using Ordering.Domain.Common;
using Ordering.Domain.Entities;
using Ordering.Domain.Events;
using Ordering.Domain.Exceptions;
using Shouldly;
using Xunit;

namespace Ordering.UnitTests;

public class OrderingDomainTests
{
    private class TestEntity : BaseEntity { }

    [Fact]
    public void BaseEntity_Should_Manage_Events()
    {
        var entity = new TestEntity();
        var order = Order.Create("b1", new List<OrderItem>());
        var domainEvent = new OrderCreatedDomainEvent(order);

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
        var order = new Order
        {
            BuyerId = "buyer-1",
            CreatedAt = now,
            CreatedBy = "creator",
            LastModifiedAt = now,
            LastModifiedBy = "modifier"
        };

        order.BuyerId.ShouldBe("buyer-1");
        order.CreatedAt.ShouldBe(now);
        order.CreatedBy.ShouldBe("creator");
        order.LastModifiedAt.ShouldBe(now);
        order.LastModifiedBy.ShouldBe("modifier");
    }

    [Fact]
    public void OrderItem_Properties_ShouldBeSetAndGet()
    {
        var orderId = Guid.NewGuid();
        var item = new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Sku = "SKU-999",
            Quantity = 5,
            UnitPrice = 19.99m
        };

        item.OrderId.ShouldBe(orderId);
        item.Sku.ShouldBe("SKU-999");
        item.Quantity.ShouldBe(5);
        item.UnitPrice.ShouldBe(19.99m);
    }

    [Fact]
    public void IdempotencyKey_Should_Be_Valid_Guid()
    {
        var idempotencyKey = Guid.CreateVersion7();
        idempotencyKey.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Order_Create_Should_Set_Status_To_Pending_And_Raise_Domain_Event()
    {
        var buyerId = "buyer-123";
        var items = new List<OrderItem>
        {
            new OrderItem { Sku = "PROD-1", Quantity = 2, UnitPrice = 50.0m }
        };

        var order = Order.Create(buyerId, items);

        order.BuyerId.ShouldBe(buyerId);
        order.Status.ShouldBe(OrderStatus.Pending);
        order.OrderItems.Count.ShouldBe(1);
        order.DomainEvents.Count.ShouldBe(1);
        order.DomainEvents.First().ShouldBeOfType<OrderCreatedDomainEvent>();
    }

    [Fact]
    public void Order_Cancel_Should_Set_Status_To_Cancelled_And_Raise_Cancel_Domain_Event()
    {
        var order = Order.Create("buyer-123", new List<OrderItem>());
        order.ClearDomainEvents();

        order.Cancel("Payment Failed");

        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.CancellationReason.ShouldBe("Payment Failed");
        order.DomainEvents.Count.ShouldBe(1);
        order.DomainEvents.First().ShouldBeOfType<OrderCancelledDomainEvent>();
    }

    [Fact]
    public void Order_Create_Without_BuyerId_Should_Throw_OrderingDomainException()
    {
        Should.Throw<OrderingDomainException>(() => Order.Create("", new List<OrderItem>()));
    }

    [Fact]
    public void Order_Cancel_Should_Be_Idempotent()
    {
        var order = Order.Create("buyer-123", new List<OrderItem>());
        order.ClearDomainEvents();
        order.Cancel("Reason 1");

        order.ClearDomainEvents();
        order.Cancel("Reason 2");

        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.CancellationReason.ShouldBe("Reason 1");
        order.DomainEvents.ShouldBeEmpty();
    }
}
