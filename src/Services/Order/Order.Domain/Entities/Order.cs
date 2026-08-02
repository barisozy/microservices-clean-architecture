using Order.Domain.Common;
using Order.Domain.Events;
using Order.Domain.Exceptions;

namespace Order.Domain.Entities;

public enum OrderStatus
{
    Pending = 1,
    Paid = 2,
    Cancelled = 3,
    Completed = 4
}

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class Order : BaseAuditableEntity
{
    public string BuyerId { get; set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid KeycloakSubject { get; private set; }
    public decimal TotalAmount { get; private set; }
    /// <summary>
    /// Plan Sprint 1: IdempotencyKey (Guid v7, caller-generated) — UNIQUE constraint on Orders.
    /// A repeated key returns the original 201 body instead of creating a second order.
    /// </summary>
    public string IdempotencyKey { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public string? CancellationReason { get; private set; }
    public List<OrderItem> OrderItems { get; set; } = new();

    public static Order Create(string buyerId, string idempotencyKey, List<OrderItem> items)
    {
        if (string.IsNullOrWhiteSpace(buyerId))
            throw new OrderDomainException("BuyerId is required to create an order.");

        var order = new Order
        {
            BuyerId = buyerId,
            IdempotencyKey = idempotencyKey,
            Status = OrderStatus.Pending,
            OrderItems = items
        };

        order.AddDomainEvent(new OrderCreatedDomainEvent(order));
        return order;
    }

    public static Order Create(
        Guid customerId,
        Guid keycloakSubject,
        string idempotencyKey,
        List<OrderItem> items)
    {
        if (customerId == Guid.Empty) throw new OrderDomainException("CustomerId is required.");
        if (keycloakSubject == Guid.Empty) throw new OrderDomainException("KeycloakSubject is required.");

        var order = Create(keycloakSubject.ToString("D"), idempotencyKey, items);
        order.CustomerId = customerId;
        order.KeycloakSubject = keycloakSubject;
        order.TotalAmount = items.Sum(item => item.Quantity * item.UnitPrice);
        return order;
    }

    public void SetTotalAmount(decimal totalAmount)
    {
        if (totalAmount < 0) throw new OrderDomainException("Order total cannot be negative.");
        TotalAmount = totalAmount;
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Cancelled) return; // Idempotent

        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        AddDomainEvent(new OrderCancelledDomainEvent(this, reason));
    }
}
