using Ordering.Domain.Common;
using Ordering.Domain.Events;
using Ordering.Domain.Exceptions;

namespace Ordering.Domain.Entities;

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
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public string? CancellationReason { get; private set; }
    public List<OrderItem> OrderItems { get; set; } = new();

    public static Order Create(string buyerId, List<OrderItem> items)
    {
        if (string.IsNullOrWhiteSpace(buyerId))
            throw new OrderingDomainException("BuyerId is required to create an order.");

        var order = new Order
        {
            BuyerId = buyerId,
            Status = OrderStatus.Pending,
            OrderItems = items
        };

        order.AddDomainEvent(new OrderCreatedDomainEvent(order));
        return order;
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Cancelled) return; // Idempotent

        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        AddDomainEvent(new OrderCancelledDomainEvent(this, reason));
    }
}
