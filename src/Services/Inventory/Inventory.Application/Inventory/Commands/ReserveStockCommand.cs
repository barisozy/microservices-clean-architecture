using Inventory.Application.Common.Interfaces;
using Inventory.Application.Common.Exceptions;
using Inventory.Domain.Entities;
using MediatR;

namespace Inventory.Application.Inventory.Commands;

public sealed record ReserveStockCommand(Guid OrderId, string Sku, int Quantity) : IRequest<(Guid ReservationId, bool Success, string Message)>;
public sealed class ReserveStockCommandHandler(
    IInventoryWriteRepository context,
    IStockReadRepository stockReadRepository,
    IInventoryReservationLeasePolicy? leasePolicy = null) : IRequestHandler<ReserveStockCommand, (Guid ReservationId, bool Success, string Message)>
{
    public async Task<(Guid ReservationId, bool Success, string Message)> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
    {
        var existing = await context.FindReservationByOrderIdAsync(request.OrderId, cancellationToken);
        if (existing is not null && existing.Items.Any(i => i.Sku == request.Sku)) return (existing.Id, true, "Stock was already reserved for this order and SKU.");
        if (existing is not null) return (Guid.Empty, false, "Order already has a reservation for different items.");
        
        var stock = await context.FindStockAsync(request.Sku, cancellationToken);
        if (stock is null) return (Guid.Empty, false, "Unknown SKU. Inventory must be provisioned before checkout.");
        if (!stock.Reserve(request.Quantity)) return (Guid.Empty, false, "Insufficient stock availability.");
        
        var now = leasePolicy?.UtcNow ?? TimeProvider.System.GetUtcNow();
        var itemsDict = new Dictionary<string, int> { { request.Sku, request.Quantity } };
        var reservation = InventoryReservation.Create(request.OrderId, itemsDict, now.AddMinutes(2));
        reservation.Commit(now);
        context.Add(reservation);
        
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (PersistenceConcurrencyException) { return (Guid.Empty, false, "Inventory changed concurrently. Retry the reservation."); }
        catch (PersistenceWriteException) { return (Guid.Empty, false, "A concurrent reservation conflict occurred. Retry the request."); }
        
        await stockReadRepository.SetAvailableQuantityAsync(stock.Sku, stock.AvailableQuantity, cancellationToken);
        return (reservation.Id, true, "Stock reserved successfully.");
    }
}
