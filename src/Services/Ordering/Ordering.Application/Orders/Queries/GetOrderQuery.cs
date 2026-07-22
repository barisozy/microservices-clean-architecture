using MediatR;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Common.Interfaces;

namespace Ordering.Application.Orders.Queries;

public record OrderStatusDto(Guid Id, string Status, string BuyerId);

public record GetOrderQuery(Guid OrderId) : IRequest<OrderStatusDto?>;

public class GetOrderQueryHandler(IOrderingDbContext context) : IRequestHandler<GetOrderQuery, OrderStatusDto?>
{
    public async Task<OrderStatusDto?> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await context.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order == null) return null;

        return new OrderStatusDto(order.Id, order.Status.ToString(), order.BuyerId);
    }
}
