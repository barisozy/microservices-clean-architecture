using MediatR;
using Microsoft.EntityFrameworkCore;
using Order.Application.Common.Interfaces;

namespace Order.Application.Orders.Queries;

public record OrderStatusDto(Guid Id, string Status, string BuyerId);

public record GetOrderQuery(Guid OrderId, string BuyerId) : IRequest<OrderStatusDto?>;

public class GetOrderQueryHandler(IOrderReadRepository readRepository) : IRequestHandler<GetOrderQuery, OrderStatusDto?>
{
    public async Task<OrderStatusDto?> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await readRepository.GetOrderAsync(request.OrderId, cancellationToken);
        return order is not null && string.Equals(order.BuyerId, request.BuyerId, StringComparison.Ordinal)
            ? order
            : null;
    }
}
