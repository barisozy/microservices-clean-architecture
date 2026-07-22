using Payments.Domain.Common;
using Payments.Domain.Entities;

namespace Payments.Domain.Events;

public class PaymentCompletedDomainEvent : BaseEvent
{
    public PaymentRecord Payment { get; }

    public PaymentCompletedDomainEvent(PaymentRecord payment)
    {
        Payment = payment;
    }
}

public class PaymentFailedDomainEvent : BaseEvent
{
    public PaymentRecord Payment { get; }
    public string Reason { get; }

    public PaymentFailedDomainEvent(PaymentRecord payment, string reason)
    {
        Payment = payment;
        Reason = reason;
    }
}
