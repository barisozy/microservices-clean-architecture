using Payments.Domain.Common;

namespace Payments.Domain.Entities;

public class PaymentRecord : BaseAuditableEntity
{
    public Guid OrderId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending";
    public string? FailureReason { get; set; }
    public string TransactionId { get; set; } = string.Empty;

    public static PaymentRecord Create(Guid orderId, string idempotencyKey, decimal amount)
    {
        return new PaymentRecord
        {
            OrderId = orderId,
            IdempotencyKey = idempotencyKey,
            Amount = amount,
            Status = "Pending",
            TransactionId = Guid.NewGuid().ToString()
        };
    }

    public void Fail(string reason)
    {
        Status = "Failed";
        FailureReason = reason;
    }

    public void Complete()
    {
        Status = "Completed";
    }
}

// Alias for Payment
public class Payment : PaymentRecord
{
    public static new Payment Create(Guid orderId, string idempotencyKey, decimal amount)
    {
        return new Payment
        {
            OrderId = orderId,
            IdempotencyKey = idempotencyKey,
            Amount = amount,
            Status = "Pending",
            TransactionId = Guid.NewGuid().ToString()
        };
    }
}
