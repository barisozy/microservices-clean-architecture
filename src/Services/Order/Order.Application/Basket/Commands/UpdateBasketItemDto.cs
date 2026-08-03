namespace Order.Application.Basket.Commands;

public sealed record UpdateBasketItemDto(string Sku, int Quantity);
