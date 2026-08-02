using MediatR;
using Order.Application.Common.Interfaces;

namespace Order.Application.Basket.Commands;

public record UpdateBasketItemDto(string Sku, int Quantity);

// GET basket query
public record GetBasketQuery(string BuyerId) : IRequest<Dictionary<string, int>>;

public class GetBasketQueryHandler(IBasketService basketService) : IRequestHandler<GetBasketQuery, Dictionary<string, int>>
{
    public async Task<Dictionary<string, int>> Handle(GetBasketQuery request, CancellationToken cancellationToken)
        => await basketService.GetBasketAsync(request.BuyerId, cancellationToken);
}

// PUT basket command
public record UpdateBasketCommand(string BuyerId, List<UpdateBasketItemDto> Items) : IRequest<bool>;

public class UpdateBasketCommandHandler(IBasketService basketService) : IRequestHandler<UpdateBasketCommand, bool>
{
    public async Task<bool> Handle(UpdateBasketCommand request, CancellationToken cancellationToken)
        => await basketService.SetBasketAsync(request.BuyerId, request.Items.ToDictionary(i => i.Sku, i => i.Quantity), cancellationToken);
}

// DELETE basket command
public record DeleteBasketCommand(string BuyerId) : IRequest<bool>;

public class DeleteBasketCommandHandler(IBasketService basketService) : IRequestHandler<DeleteBasketCommand, bool>
{
    public async Task<bool> Handle(DeleteBasketCommand request, CancellationToken cancellationToken)
        => await basketService.DeleteBasketAsync(request.BuyerId, cancellationToken);
}

