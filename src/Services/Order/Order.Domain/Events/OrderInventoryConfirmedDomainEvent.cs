using Order.Domain.Common;

namespace Order.Domain.Events;

public sealed class OrderInventoryConfirmedDomainEvent(global::Order.Domain.Entities.Order order) : BaseEvent
{
    public global::Order.Domain.Entities.Order Order { get; } = order;
}
