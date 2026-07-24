namespace ECommerce.Contracts.Events.v1;

public record StockReserved(
    Guid OrderId,
    Guid CustomerId,
    string IdempotencyKey,
    List<OrderItemContractDto> Items,
    decimal TotalAmount,
    DateTimeOffset ReservedAt,
    DateTimeOffset OrderCreatedAt
);
