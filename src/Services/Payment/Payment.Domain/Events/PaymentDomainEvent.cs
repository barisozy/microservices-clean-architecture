using Payment.Domain.Entities;

namespace Payment.Domain.Events;

public abstract class PaymentDomainEvent(PaymentRecord payment) : Payment.Domain.Common.BaseEvent
{
    public PaymentRecord Payment { get; } = payment;
}
