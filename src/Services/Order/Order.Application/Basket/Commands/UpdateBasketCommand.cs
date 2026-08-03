using MediatR;
using Order.Application.Common.Interfaces;

namespace Order.Application.Basket.Commands;

public sealed record UpdateBasketCommand(string BuyerId, List<UpdateBasketItemDto> Items) : IRequest<bool>;

public sealed class UpdateBasketCommandHandler(IBasketService basketService)
    : IRequestHandler<UpdateBasketCommand, bool>
{
    public async Task<bool> Handle(UpdateBasketCommand request, CancellationToken cancellationToken)
    {
        await basketService.SetBasketAsync(
            request.BuyerId,
            request.Items.ToDictionary(item => item.Sku, item => item.Quantity),
            cancellationToken);
        return true;
    }
}
