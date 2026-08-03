using Order.Domain.Common;
using Order.Domain.Entities;

namespace Order.Domain.Events;

public sealed class OrderCreatedDomainEvent : BaseEvent
{
    public Entities.Order Order { get; }

    public OrderCreatedDomainEvent(Entities.Order order)
    {
        Order = order;
    }
}
