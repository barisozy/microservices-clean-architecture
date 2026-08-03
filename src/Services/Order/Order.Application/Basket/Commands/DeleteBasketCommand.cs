using MediatR;
using Order.Application.Common.Interfaces;

namespace Order.Application.Basket.Commands;

public sealed record DeleteBasketCommand(string BuyerId) : IRequest<bool>;

public sealed class DeleteBasketCommandHandler(IBasketService basketService)
    : IRequestHandler<DeleteBasketCommand, bool>
{
    public async Task<bool> Handle(DeleteBasketCommand request, CancellationToken cancellationToken)
    {
        await basketService.DeleteBasketAsync(request.BuyerId, cancellationToken);
        return true;
    }
}
