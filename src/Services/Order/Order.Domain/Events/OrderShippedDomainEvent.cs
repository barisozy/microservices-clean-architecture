using Order.Domain.Common;

namespace Order.Domain.Events;

public sealed class OrderShippedDomainEvent : BaseEvent
{
    public Entities.Order Order { get; }

    public OrderShippedDomainEvent(Entities.Order order)
    {
        Order = order;
    }
}
