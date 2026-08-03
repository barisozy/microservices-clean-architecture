using System.ComponentModel.DataAnnotations.Schema;

namespace Fulfillment.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    private readonly List<object> _domainEvents = new();
    [NotMapped] public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();
    public void AddDomainEvent(object domainEvent) => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(object domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
