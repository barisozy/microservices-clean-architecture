using Inventory.Application.Common.Interfaces;
using Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Inventory.Commands;

public sealed record SetStockCommand(string Sku, int Quantity) : IRequest<int>;
public sealed class SetStockCommandHandler(IInventoryDbContext context, IStockReadRepository stockReadRepository) : IRequestHandler<SetStockCommand, int>
{
    public async Task<int> Handle(SetStockCommand request, CancellationToken cancellationToken)
    {
        var stock = await context.Stocks.FirstOrDefaultAsync(candidate => candidate.Sku == request.Sku, cancellationToken);
        if (stock is null) { stock = new Stock(request.Sku, request.Quantity); context.Stocks.Add(stock); }
        else stock.SetQuantity(request.Quantity);
        await context.SaveChangesAsync(cancellationToken);
        await stockReadRepository.SetAvailableQuantityAsync(stock.Sku, stock.AvailableQuantity, cancellationToken);
        return stock.AvailableQuantity;
    }
}
