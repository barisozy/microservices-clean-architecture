using ECommerce.Contracts.Events;
using Inventory.Application.Common.Interfaces;
using Inventory.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Inventory.Commands;

public record ReserveStockCommand(Guid OrderId, string Sku, int Quantity) : IRequest<(Guid ReservationId, bool Success, string Message)>;

public class ReserveStockCommandHandler(IInventoryDbContext context) : IRequestHandler<ReserveStockCommand, (Guid ReservationId, bool Success, string Message)>
{
    public async Task<(Guid ReservationId, bool Success, string Message)> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
    {
        var stock = await context.Stocks.FirstOrDefaultAsync(s => s.Sku == request.Sku, cancellationToken);
        if (stock == null)
        {
            stock = new Stock(request.Sku, 1000);
            context.Stocks.Add(stock);
        }

        if (!stock.Reserve(request.Quantity))
        {
            return (Guid.Empty, false, "Insufficient stock availability.");
        }

        var reservation = InventoryReservation.Create(request.OrderId, request.Sku, request.Quantity);
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync(cancellationToken);

        return (reservation.Id, true, "Stock reserved successfully.");
    }
}

public record ReleaseStockCommand(Guid ReservationId) : IRequest<bool>;

public class ReleaseStockCommandHandler(IInventoryDbContext context, IPublishEndpoint publishEndpoint) : IRequestHandler<ReleaseStockCommand, bool>
{
    public async Task<bool> Handle(ReleaseStockCommand request, CancellationToken cancellationToken)
    {
        var reservation = await context.Reservations.FirstOrDefaultAsync(r => r.Id == request.ReservationId, cancellationToken);
        if (reservation == null) return false;

        reservation.Release();
        var stock = await context.Stocks.FirstOrDefaultAsync(s => s.Sku == reservation.Sku, cancellationToken);
        stock?.Release(reservation.Quantity);

        await context.SaveChangesAsync(cancellationToken);
        await publishEndpoint.Publish(new StockReleasedEvent(reservation.Id, DateTimeOffset.UtcNow), cancellationToken);

        return true;
    }
}

public record GetStockAvailabilityQuery(string Sku) : IRequest<int>;

public class GetStockAvailabilityQueryHandler(IInventoryDbContext context) : IRequestHandler<GetStockAvailabilityQuery, int>
{
    public async Task<int> Handle(GetStockAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var stock = await context.Stocks.FirstOrDefaultAsync(s => s.Sku == request.Sku, cancellationToken);
        return stock?.AvailableQuantity ?? 0;
    }
}

