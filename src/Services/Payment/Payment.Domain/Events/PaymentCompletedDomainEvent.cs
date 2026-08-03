using Payment.Domain.Entities;

namespace Payment.Domain.Events;

public sealed class PaymentCompletedDomainEvent(PaymentRecord payment) : PaymentDomainEvent(payment);
