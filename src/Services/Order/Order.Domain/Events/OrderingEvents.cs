using Order.Domain.Common;
using Order.Domain.Entities;

namespace Order.Domain.Events;

public class OrderCreatedDomainEvent : BaseEvent
{
    public Entities.Order Order { get; }

    public OrderCreatedDomainEvent(Entities.Order order)
    {
        Order = order;
    }
}

public class OrderCancelledDomainEvent : BaseEvent
{
    public Entities.Order Order { get; }
    public string Reason { get; }

    public OrderCancelledDomainEvent(Entities.Order order, string reason)
    {
        Order = order;
        Reason = reason;
    }
}
