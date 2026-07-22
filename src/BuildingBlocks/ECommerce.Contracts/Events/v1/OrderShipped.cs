namespace ECommerce.Contracts.Events.v1;

public record OrderShipped(
    Guid OrderId,
    string TrackingId,
    DateTimeOffset ShippedAt
);
