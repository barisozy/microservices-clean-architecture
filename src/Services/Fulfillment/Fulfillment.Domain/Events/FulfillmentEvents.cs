using Fulfillment.Domain.Common;
using Fulfillment.Domain.Entities;

namespace Fulfillment.Domain.Events;

public class OrderShippedDomainEvent : BaseEvent
{
    public FulfillmentTask Task { get; }

    public OrderShippedDomainEvent(FulfillmentTask task)
    {
        Task = task;
    }
}
