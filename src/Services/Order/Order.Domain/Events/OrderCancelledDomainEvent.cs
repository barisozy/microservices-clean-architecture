using Order.Domain.Common;

namespace Order.Domain.Events;

public sealed class OrderCancelledDomainEvent : BaseEvent
{
    public Entities.Order Order { get; }
    public string Reason { get; }

    public OrderCancelledDomainEvent(Entities.Order order, string reason)
    {
        Order = order;
        Reason = reason;
    }
}
