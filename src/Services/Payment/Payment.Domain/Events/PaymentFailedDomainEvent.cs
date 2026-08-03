using Payment.Domain.Entities;

namespace Payment.Domain.Events;

public sealed class PaymentFailedDomainEvent(PaymentRecord payment, string reason) : PaymentDomainEvent(payment)
{
    public string Reason { get; } = reason;
}
