namespace ECommerce.Contracts.Events;

public record OrderItemContractDto(string Sku, int Quantity, decimal UnitPrice);

public record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    string IdempotencyKey,
    List<OrderItemContractDto> Items,
    decimal TotalAmount,
    DateTimeOffset CreatedAt
);

public record PaymentCompletedEvent(
    Guid OrderId,
    Guid PaymentId,
    string IdempotencyKey,
    DateTimeOffset CompletedAt
);

public record OrderShippedEvent(
    Guid OrderId,
    string TrackingId,
    DateTimeOffset ShippedAt
);

public record PaymentFailedEvent(
    Guid OrderId,
    string IdempotencyKey,
    string Reason,
    DateTimeOffset FailedAt
);

public record OrderCancelledEvent(
    Guid OrderId,
    string Reason,
    DateTimeOffset CancelledAt
);

public record StockReleasedEvent(
    Guid ReservationId,
    DateTimeOffset ReleasedAt
);

