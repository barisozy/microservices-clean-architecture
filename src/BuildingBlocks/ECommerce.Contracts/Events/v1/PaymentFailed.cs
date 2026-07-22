namespace ECommerce.Contracts.Events.v1;

public record PaymentFailed(
    Guid OrderId,
    string IdempotencyKey,
    string Reason,
    DateTimeOffset FailedAt
);
