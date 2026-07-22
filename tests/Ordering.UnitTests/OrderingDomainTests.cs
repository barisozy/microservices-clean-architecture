using Ordering.Domain.Entities;
using Ordering.Domain.Events;
using Ordering.Domain.Exceptions;
using Shouldly;
using Xunit;

namespace Ordering.UnitTests;

public class OrderingDomainTests
{
    private class TestEntity : Ordering.Domain.Common.BaseEntity { }

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
    public void IdempotencyKey_Should_Be_Valid_Guid()
    {
        // Arrange
        var idempotencyKey = Guid.CreateVersion7();

        // Assert
        idempotencyKey.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Order_Create_Should_Set_Status_To_Pending_And_Raise_Domain_Event()
    {
        // Arrange
        var buyerId = "buyer-123";
        var items = new List<OrderItem>
        {
            new OrderItem { Sku = "PROD-1", Quantity = 2, UnitPrice = 50.0m }
        };

        // Act
        var order = Order.Create(buyerId, items);

        // Assert
        order.BuyerId.ShouldBe(buyerId);
        order.Status.ShouldBe(OrderStatus.Pending);
        order.OrderItems.Count.ShouldBe(1);
        order.DomainEvents.Count.ShouldBe(1);
        order.DomainEvents.First().ShouldBeOfType<OrderCreatedDomainEvent>();
    }

    [Fact]
    public void Order_Cancel_Should_Set_Status_To_Cancelled_And_Raise_Cancel_Domain_Event()
    {
        // Arrange
        var order = Order.Create("buyer-123", new List<OrderItem>());
        order.ClearDomainEvents();

        // Act
        order.Cancel("Payment Failed");

        // Assert
        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.CancellationReason.ShouldBe("Payment Failed");
        order.DomainEvents.Count.ShouldBe(1);
        order.DomainEvents.First().ShouldBeOfType<OrderCancelledDomainEvent>();
    }

    [Fact]
    public void Order_Create_Without_BuyerId_Should_Throw_OrderingDomainException()
    {
        // Act & Assert
        Should.Throw<OrderingDomainException>(() => Order.Create("", new List<OrderItem>()));
    }

    [Fact]
    public void Order_Cancel_Should_Be_Idempotent()
    {
        // Arrange
        var order = Order.Create("buyer-123", new List<OrderItem>());
        order.ClearDomainEvents();
        order.Cancel("Reason 1");

        // Act
        order.ClearDomainEvents();
        order.Cancel("Reason 2");

        // Assert
        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.CancellationReason.ShouldBe("Reason 1"); // Reason shouldn't change
        order.DomainEvents.ShouldBeEmpty(); // Event shouldn't be added again
    }
}
