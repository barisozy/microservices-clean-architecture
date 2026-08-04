using Order.Application.Common.Interfaces;
using Order.Domain.Common;
using Order.Domain.Events;

namespace Order.Application.Orders.EventHandlers;

internal sealed class DomainEventDispatcher(
    IEnumerable<IDomainEventHandler<OrderCreatedDomainEvent>> createdHandlers,
    IEnumerable<IDomainEventHandler<OrderInventoryConfirmedDomainEvent>> inventoryConfirmedHandlers,
    IEnumerable<IDomainEventHandler<OrderCancelledDomainEvent>> cancelledHandlers,
    IEnumerable<IDomainEventHandler<OrderPaidDomainEvent>> paidHandlers,
    IEnumerable<IDomainEventHandler<OrderShippedDomainEvent>> shippedHandlers)
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
            case OrderInventoryConfirmedDomainEvent inventoryConfirmed:
                foreach (var handler in inventoryConfirmedHandlers)
                    await handler.Handle(inventoryConfirmed, cancellationToken);
                break;
            case OrderCancelledDomainEvent cancelled:
                foreach (var handler in cancelledHandlers)
                    await handler.Handle(cancelled, cancellationToken);
                break;
            case OrderPaidDomainEvent paid:
                foreach (var handler in paidHandlers)
                    await handler.Handle(paid, cancellationToken);
                break;
            case OrderShippedDomainEvent shipped:
                foreach (var handler in shippedHandlers)
                    await handler.Handle(shipped, cancellationToken);
                break;
            default:
                throw new InvalidOperationException(
                    $"No domain event dispatch route is registered for {domainEvent.GetType().Name}.");
        }
    }
}
