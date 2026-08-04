namespace ECommerce.Contracts.Events.v1;

public sealed record CheckoutStarted(
    Guid OrderId,
    Guid CustomerId,
    string IdempotencyKey,
    List<OrderItemContractDto> Items,
    decimal TotalAmount,
    DateTimeOffset OccurredAt);

public sealed record ReserveInventory(
    Guid OrderId,
    List<OrderItemContractDto> Items);

public sealed record InventoryReserved(
    Guid OrderId,
    Guid ReservationId,
    DateTimeOffset ExpiresAt);

public sealed record InventoryReservationRejected(Guid OrderId, string Reason);

public sealed record CommitInventoryReservation(Guid OrderId);

public sealed record InventoryReservationCommitted(Guid OrderId, Guid ReservationId);

public sealed record OrderInventoryConfirmed(Guid OrderId);

public sealed record ReleaseInventoryReservation(Guid OrderId, string Reason);

public sealed record InventoryReservationReleased(Guid OrderId, string Reason);

public sealed record InventoryReservationExpired(Guid OrderId, DateTimeOffset ExpiredAt);

public sealed record CheckoutTimedOut(Guid OrderId);
