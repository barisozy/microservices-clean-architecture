using Order.Domain.Common;

namespace Order.Application.Common.Interfaces;

public interface IDomainEventDispatcher
{
    Task Dispatch(BaseEvent domainEvent, CancellationToken cancellationToken);
}
