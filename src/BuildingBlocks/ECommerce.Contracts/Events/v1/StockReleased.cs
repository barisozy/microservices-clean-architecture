namespace ECommerce.Contracts.Events.v1;

public record StockReleased(
    Guid OrderId,
    Guid ReservationId,
    DateTimeOffset ReleasedAt
);
