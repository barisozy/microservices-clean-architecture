namespace Payment.Domain.Entities;

public sealed class Payment : PaymentRecord
{
    public static new Payment Create(Guid orderId, string idempotencyKey, decimal amount) => new()
    {
        OrderId = orderId, IdempotencyKey = idempotencyKey, Amount = amount,
        Status = "Pending", TransactionId = Guid.NewGuid().ToString()
    };
}
