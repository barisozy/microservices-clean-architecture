using Order.Domain.Common;

namespace Order.Application.Common.Interfaces;

public interface IDomainEventHandler<in TEvent> where TEvent : BaseEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}
