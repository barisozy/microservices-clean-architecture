namespace Order.Application.Orders.Queries;

public sealed record OrderStatusDto(Guid Id, string Status, string BuyerId);
