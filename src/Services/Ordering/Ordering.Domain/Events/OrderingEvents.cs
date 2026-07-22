using Ordering.Domain.Common;
using Ordering.Domain.Entities;

namespace Ordering.Domain.Events;

public class OrderCreatedDomainEvent : BaseEvent
{
    public Order Order { get; }

    public OrderCreatedDomainEvent(Order order)
    {
        Order = order;
    }
}

public class OrderCancelledDomainEvent : BaseEvent
{
    public Order Order { get; }
    public string Reason { get; }

    public OrderCancelledDomainEvent(Order order, string reason)
    {
        Order = order;
        Reason = reason;
    }
}
