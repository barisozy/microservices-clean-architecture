using Inventory.Application.Common.Interfaces;
using Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Inventory.Commands;

public sealed record ReserveStockCommand(Guid OrderId, string Sku, int Quantity) : IRequest<(Guid ReservationId, bool Success, string Message)>;
public sealed class ReserveStockCommandHandler(
    IInventoryDbContext context,
    IStockReadRepository stockReadRepository,
    IInventoryReservationLeasePolicy? leasePolicy = null) : IRequestHandler<ReserveStockCommand, (Guid ReservationId, bool Success, string Message)>
{
    public async Task<(Guid ReservationId, bool Success, string Message)> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
    {
        var reservations = context.Reservations;
        var existing = reservations is null
            ? null
            : await reservations.FirstOrDefaultAsync(r => r.OrderId == request.OrderId && r.Sku == request.Sku, cancellationToken);
        if (existing is not null) return (existing.Id, true, "Stock was already reserved for this order and SKU.");
        var stock = await context.Stocks.FirstOrDefaultAsync(s => s.Sku == request.Sku, cancellationToken);
        if (stock is null) return (Guid.Empty, false, "Unknown SKU. Inventory must be provisioned before checkout.");
        if (!stock.Reserve(request.Quantity)) return (Guid.Empty, false, "Insufficient stock availability.");
        // This legacy synchronous gRPC operation is not part of checkout.
        // It commits its reservation locally so the lease reaper cannot release
        // a reservation that has no saga to send CommitInventoryReservation.
        var now = leasePolicy?.UtcNow ?? TimeProvider.System.GetUtcNow();
        var reservation = InventoryReservation.Create(request.OrderId, request.Sku, request.Quantity, now.AddMinutes(2));
        reservation.Commit(now);
        context.Reservations.Add(reservation);
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return (Guid.Empty, false, "Inventory changed concurrently. Retry the reservation."); }
        catch (DbUpdateException) { return (Guid.Empty, false, "A concurrent reservation conflict occurred. Retry the request."); }
        await stockReadRepository.SetAvailableQuantityAsync(stock.Sku, stock.AvailableQuantity, cancellationToken);
        return (reservation.Id, true, "Stock reserved successfully.");
    }
}
