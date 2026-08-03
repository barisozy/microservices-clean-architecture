using Order.Domain.Common;

namespace Order.Domain.Events;

public sealed class OrderPaidDomainEvent : BaseEvent
{
    public Entities.Order Order { get; }

    public OrderPaidDomainEvent(Entities.Order order)
    {
        Order = order;
    }
}
