using ECommerce.Contracts.Events.v1;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Common.Exceptions;
using MediatR;
using MassTransit;
using Inventory.Domain.Entities;

namespace Inventory.Application.Inventory.Commands;

public sealed record ReleaseStockCommand(Guid ReservationId) : IRequest<bool>;
public sealed class ReleaseStockCommandHandler(
    IInventoryWriteRepository context,
    IPublishEndpoint publishEndpoint,
    IStockReadRepository stockReadRepository,
    TimeProvider? timeProvider = null) : IRequestHandler<ReleaseStockCommand, bool>
{
    public async Task<bool> Handle(ReleaseStockCommand request, CancellationToken cancellationToken)
    {
        var reservation = await context.FindReservationAsync(request.ReservationId, cancellationToken);
        if (reservation is null) return false;
        if (reservation.Status is InventoryReservationStatus.Released or InventoryReservationStatus.Expired) return true;
        
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        reservation.Release(now);
        
        foreach(var item in reservation.Items)
        {
            var stock = await context.FindStockAsync(item.Sku, cancellationToken);
            if (stock is not null)
            {
                stock.Release(item.Quantity);
                await stockReadRepository.SetAvailableQuantityAsync(stock.Sku, stock.AvailableQuantity, cancellationToken);
            }
        }
        
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (PersistenceConcurrencyException) { return false; }
        
        await publishEndpoint.Publish(new StockReleased(reservation.OrderId, reservation.Id, now), cancellationToken);
        return true;
    }
}
