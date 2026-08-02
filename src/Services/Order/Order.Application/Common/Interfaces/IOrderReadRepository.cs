using Order.Application.Orders.Queries;

namespace Order.Application.Common.Interfaces;

public interface IOrderReadRepository
{
    Task<OrderStatusDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task SetOrderAsync(OrderStatusDto order, CancellationToken cancellationToken);
}
