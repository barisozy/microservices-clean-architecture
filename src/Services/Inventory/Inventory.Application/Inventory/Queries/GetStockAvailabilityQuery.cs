using Inventory.Application.Common.Interfaces;
using MediatR;

namespace Inventory.Application.Inventory.Commands;

public sealed record GetStockAvailabilityQuery(string Sku) : IRequest<int>;
public sealed class GetStockAvailabilityQueryHandler(IStockReadRepository stockReadRepository) : IRequestHandler<GetStockAvailabilityQuery, int>
{
    public async Task<int> Handle(GetStockAvailabilityQuery request, CancellationToken cancellationToken) =>
        await stockReadRepository.GetAvailableQuantityAsync(request.Sku, cancellationToken) ?? 0;
}
