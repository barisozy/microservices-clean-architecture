using System;
using System.Collections.Generic;
using Order.Domain.Entities;
using Order.Domain.Enums;
using Order.Domain.Exceptions;
using Shouldly;
using Xunit;

namespace Order.UnitTests;

public class OrderEntityTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateOrderAndRaiseEvent()
    {
        var order = global::Order.Domain.Entities.Order.Create("buyer1", "key1", new List<OrderItem>());
        order.BuyerId.ShouldBe("buyer1");
        order.IdempotencyKey.ShouldBe("key1");
        order.Status.ShouldBe(OrderStatus.PendingInventory);
        order.DomainEvents.Count.ShouldBe(1);
    }

    [Fact]
    public void Create_WithEmptyBuyerId_ShouldThrowException()
    {
        Should.Throw<OrderDomainException>(() =>
            global::Order.Domain.Entities.Order.Create("", "key1", new List<OrderItem>()));
    }

    [Fact]
    public void Create_WithFullData_ShouldSetTotalAmount()
    {
        var customerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var items = new List<OrderItem> { new OrderItem { Sku = "SKU1", Quantity = 2, UnitPrice = 100m } };

        var order = global::Order.Domain.Entities.Order.Create(customerId, subjectId, "key1", items);
        
        order.CustomerId.ShouldBe(customerId);
        order.KeycloakSubject.ShouldBe(subjectId);
        order.TotalAmount.ShouldBe(200m);
    }

    [Fact]
    public void Create_WithEmptyCustomerId_ShouldThrowException()
    {
        Should.Throw<OrderDomainException>(() =>
            global::Order.Domain.Entities.Order.Create(Guid.Empty, Guid.NewGuid(), "key1", new List<OrderItem>()));
    }

    [Fact]
    public void Create_WithEmptySubjectId_ShouldThrowException()
    {
        Should.Throw<OrderDomainException>(() =>
            global::Order.Domain.Entities.Order.Create(Guid.NewGuid(), Guid.Empty, "key1", new List<OrderItem>()));
    }

    [Fact]
    public void SetTotalAmount_NegativeValue_ShouldThrowException()
    {
        var order = global::Order.Domain.Entities.Order.Create("buyer1", "key1", new List<OrderItem>());
        Should.Throw<OrderDomainException>(() => order.SetTotalAmount(-1m));
    }

    [Fact]
    public void ConfirmInventory_WhenPendingInventory_ShouldChangeStatus()
    {
        var order = global::Order.Domain.Entities.Order.Create("buyer1", "key1", new List<OrderItem>());
        order.ConfirmInventory();
        order.Status.ShouldBe(OrderStatus.AwaitingPayment);
    }

    [Fact]
    public void ConfirmInventory_WhenAlreadyAwaitingPayment_ShouldBeIdempotent()
    {
        var order = global::Order.Domain.Entities.Order.Create("buyer1", "key1", new List<OrderItem>());
        order.ConfirmInventory();
        var eventCount = order.DomainEvents.Count;
        
        order.ConfirmInventory(); // idempotent
        
        order.Status.ShouldBe(OrderStatus.AwaitingPayment);
        order.DomainEvents.Count.ShouldBe(eventCount);
    }

    [Fact]
    public void MarkAsPaid_WhenAwaitingPayment_ShouldChangeStatus()
    {
        var order = global::Order.Domain.Entities.Order.Create("buyer1", "key1", new List<OrderItem>());
        order.ConfirmInventory();
        order.MarkAsPaid();
        order.Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public void MarkAsPaid_WhenAlreadyPaid_ShouldBeIdempotent()
    {
        var order = global::Order.Domain.Entities.Order.Create("buyer1", "key1", new List<OrderItem>());
        order.ConfirmInventory();
        order.MarkAsPaid();
        var eventCount = order.DomainEvents.Count;
        
        order.MarkAsPaid(); // idempotent
        order.Status.ShouldBe(OrderStatus.Paid);
        order.DomainEvents.Count.ShouldBe(eventCount);
    }

    [Fact]
    public void MarkAsPaid_WhenShipped_ShouldBeIdempotent()
    {
        var order = global::Order.Domain.Entities.Order.Create("buyer1", "key1", new List<OrderItem>());
        order.ConfirmInventory();
        order.MarkAsPaid();
        order.MarkAsShipped();
        
        var eventCount = order.DomainEvents.Count;
        order.MarkAsPaid(); // idempotent after shipped
        order.DomainEvents.Count.ShouldBe(eventCount);
    }

    [Fact]
    public void MarkAsShipped_WhenPaid_ShouldChangeStatus()
    {
        var order = global::Order.Domain.Entities.Order.Create("buyer1", "key1", new List<OrderItem>());
        order.ConfirmInventory();
        order.MarkAsPaid();
        order.MarkAsShipped();
        order.Status.ShouldBe(OrderStatus.Shipped);
    }

    [Fact]
    public void MarkAsShipped_WhenNotPaid_ShouldThrowException()
    {
        var order = global::Order.Domain.Entities.Order.Create("buyer1", "key1", new List<OrderItem>());
        Should.Throw<OrderDomainException>(() => order.MarkAsShipped());
    }

    [Fact]
    public void Cancel_WithReason_ShouldChangeStatus()
    {
        var order = global::Order.Domain.Entities.Order.Create("buyer1", "key1", new List<OrderItem>());
        order.Cancel("out of stock");
        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.CancellationReason.ShouldBe("out of stock");
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ShouldBeIdempotent()
    {
        var order = global::Order.Domain.Entities.Order.Create("buyer1", "key1", new List<OrderItem>());
        order.Cancel("reason 1");
        var eventCount = order.DomainEvents.Count;
        
        order.Cancel("reason 2"); // idempotent
        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.CancellationReason.ShouldBe("reason 1");
        order.DomainEvents.Count.ShouldBe(eventCount);
    }
}
