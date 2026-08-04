using ECommerce.Contracts.Events.v1;
using Inventory.Application.Common.Interfaces;
using MediatR;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Inventory.Domain.Entities;

namespace Inventory.Application.Inventory.Commands;

public sealed record ReleaseStockCommand(Guid ReservationId) : IRequest<bool>;
public sealed class ReleaseStockCommandHandler(
    IInventoryDbContext context,
    IPublishEndpoint publishEndpoint,
    IStockReadRepository stockReadRepository,
    TimeProvider? timeProvider = null) : IRequestHandler<ReleaseStockCommand, bool>
{
    public async Task<bool> Handle(ReleaseStockCommand request, CancellationToken cancellationToken)
    {
        var reservation = await context.Reservations.FirstOrDefaultAsync(r => r.Id == request.ReservationId, cancellationToken);
        if (reservation is null) return false;
        if (reservation.Status is InventoryReservationStatus.Released or InventoryReservationStatus.Expired) return true;
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        reservation.Release(now);
        var stock = await context.Stocks.FirstOrDefaultAsync(s => s.Sku == reservation.Sku, cancellationToken);
        stock?.Release(reservation.Quantity);
        await publishEndpoint.Publish(new StockReleased(reservation.OrderId, reservation.Id, now), cancellationToken);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return false; }
        if (stock is not null) await stockReadRepository.SetAvailableQuantityAsync(stock.Sku, stock.AvailableQuantity, cancellationToken);
        return true;
    }
}
