namespace Order.Application.Orders.Commands.CreateOrder;

public sealed record OrderItemDto(string Sku, int Quantity, decimal UnitPrice);
