using MediatR;
using Order.Application.Common.Interfaces;

namespace Order.Application.Basket.Commands;

public sealed record GetBasketQuery(string BuyerId) : IRequest<Dictionary<string, int>>;

public sealed class GetBasketQueryHandler(IBasketService basketService)
    : IRequestHandler<GetBasketQuery, Dictionary<string, int>>
{
    public Task<Dictionary<string, int>> Handle(GetBasketQuery request, CancellationToken cancellationToken) =>
        basketService.GetBasketAsync(request.BuyerId, cancellationToken);
}
