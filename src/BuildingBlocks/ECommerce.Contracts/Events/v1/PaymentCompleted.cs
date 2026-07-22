namespace ECommerce.Contracts.Events.v1;

public record PaymentCompleted(
    Guid OrderId,
    Guid PaymentId,
    string IdempotencyKey,
    DateTimeOffset CompletedAt
);
