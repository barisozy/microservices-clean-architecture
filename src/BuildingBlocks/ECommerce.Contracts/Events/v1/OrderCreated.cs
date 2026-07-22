namespace ECommerce.Contracts.Events.v1;

public record OrderCreated(
    Guid OrderId,
    Guid CustomerId,
    string IdempotencyKey,
    List<OrderItemContractDto> Items,
    decimal TotalAmount,
    DateTimeOffset CreatedAt
);
