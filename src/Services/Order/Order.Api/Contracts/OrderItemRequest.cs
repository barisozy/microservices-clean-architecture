namespace Order.Api.Contracts;

public sealed record OrderItemRequest(string Sku, int Quantity, decimal UnitPrice);
