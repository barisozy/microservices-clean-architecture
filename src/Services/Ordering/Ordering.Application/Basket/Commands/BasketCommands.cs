using MediatR;
using Ordering.Application.Common.Interfaces;

namespace Ordering.Application.Basket.Commands;

public record UpdateBasketItemDto(string Sku, int Quantity);

public record UpdateBasketCommand(string BuyerId, List<UpdateBasketItemDto> Items) : IRequest<bool>;

public class UpdateBasketCommandHandler : IRequestHandler<UpdateBasketCommand, bool>
{
    private readonly IBasketService _basketService;

    public UpdateBasketCommandHandler(IBasketService basketService)
    {
        _basketService = basketService;
    }

    public Task<bool> Handle(UpdateBasketCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_basketService != null);
    }
}

public record DeleteBasketCommand(string BuyerId) : IRequest<bool>;

public class DeleteBasketCommandHandler(IBasketService basketService) : IRequestHandler<DeleteBasketCommand, bool>
{
    public async Task<bool> Handle(DeleteBasketCommand request, CancellationToken cancellationToken)
    {
        return await basketService.DeleteBasketAsync(request.BuyerId, cancellationToken);
    }
}
