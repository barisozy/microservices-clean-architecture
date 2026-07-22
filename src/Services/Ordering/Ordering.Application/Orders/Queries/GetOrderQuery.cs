using MediatR;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Common.Interfaces;

namespace Ordering.Application.Orders.Queries;

public record OrderStatusDto(Guid Id, string Status, string BuyerId);

public record GetOrderQuery(Guid OrderId) : IRequest<OrderStatusDto?>;

public class GetOrderQueryHandler(IOrderReadRepository readRepository) : IRequestHandler<GetOrderQuery, OrderStatusDto?>
{
    public async Task<OrderStatusDto?> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        return await readRepository.GetOrderAsync(request.OrderId, cancellationToken);
    }
}
