using Order.Domain.Common;
using Order.Domain.Events;

namespace Order.Application.Common.Interfaces;

public interface IDomainEventHandler<in TEvent> where TEvent : BaseEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}

public interface IDomainEventDispatcher
{
    Task Dispatch(BaseEvent domainEvent, CancellationToken cancellationToken);
}

internal sealed class DomainEventDispatcher(
    IEnumerable<IDomainEventHandler<OrderCreatedDomainEvent>> createdHandlers,
    IEnumerable<IDomainEventHandler<OrderCancelledDomainEvent>> cancelledHandlers)
    : IDomainEventDispatcher
{
    public async Task Dispatch(BaseEvent domainEvent, CancellationToken cancellationToken)
    {
        switch (domainEvent)
        {
            case OrderCreatedDomainEvent created:
                foreach (var handler in createdHandlers)
                    await handler.Handle(created, cancellationToken);
                break;
            case OrderCancelledDomainEvent cancelled:
                foreach (var handler in cancelledHandlers)
                    await handler.Handle(cancelled, cancellationToken);
                break;
            default:
                throw new InvalidOperationException(
                    $"No domain event dispatch route is registered for {domainEvent.GetType().Name}.");
        }
    }
}
