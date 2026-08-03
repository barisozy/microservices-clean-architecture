namespace Order.Api.Contracts;

public sealed record CreateOrderRequest(List<OrderItemRequest>? Items, string? CouponCode = null);
