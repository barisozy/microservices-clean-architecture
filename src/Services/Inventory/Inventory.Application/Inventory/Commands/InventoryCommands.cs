using ECommerce.Contracts.Events.v1;
using Inventory.Application.Common.Interfaces;
using Inventory.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Inventory.Commands;

public record SetStockCommand(string Sku, int Quantity) : IRequest<int>;

public sealed class SetStockCommandHandler(
    IInventoryDbContext context,
    IStockReadRepository stockReadRepository) : IRequestHandler<SetStockCommand, int>
{
    public async Task<int> Handle(SetStockCommand request, CancellationToken cancellationToken)
    {
        var stock = await context.Stocks.FirstOrDefaultAsync(
            candidate => candidate.Sku == request.Sku,
            cancellationToken);
        if (stock is null)
        {
            stock = new Stock(request.Sku, request.Quantity);
            context.Stocks.Add(stock);
        }
        else
        {
            stock.SetQuantity(request.Quantity);
        }

        await context.SaveChangesAsync(cancellationToken);
        await stockReadRepository.SetAvailableQuantityAsync(
            stock.Sku,
            stock.AvailableQuantity,
            cancellationToken);
        return stock.AvailableQuantity;
    }
}

public record ReserveStockCommand(Guid OrderId, string Sku, int Quantity) : IRequest<(Guid ReservationId, bool Success, string Message)>;

public class ReserveStockCommandHandler(IInventoryDbContext context, IStockReadRepository stockReadRepository) : IRequestHandler<ReserveStockCommand, (Guid ReservationId, bool Success, string Message)>
{
    public async Task<(Guid ReservationId, bool Success, string Message)> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
    {
        var reservations = context.Reservations;
        var existingReservation = reservations is null
            ? null
            : await reservations.FirstOrDefaultAsync(
                reservation => reservation.OrderId == request.OrderId && reservation.Sku == request.Sku,
                cancellationToken);
        if (existingReservation is not null)
        {
            return (existingReservation.Id, true, "Stock was already reserved for this order and SKU.");
        }

        var stock = await context.Stocks.FirstOrDefaultAsync(s => s.Sku == request.Sku, cancellationToken);
        if (stock is null)
        {
            return (Guid.Empty, false, "Unknown SKU. Inventory must be provisioned before checkout.");
        }

        if (!stock.Reserve(request.Quantity))
        {
            return (Guid.Empty, false, "Insufficient stock availability.");
        }

        var reservation = InventoryReservation.Create(request.OrderId, request.Sku, request.Quantity);
        context.Reservations.Add(reservation);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (Guid.Empty, false, "Inventory changed concurrently. Retry the reservation.");
        }
        catch (DbUpdateException)
        {
            return (Guid.Empty, false, "A concurrent reservation conflict occurred. Retry the request.");
        }

        // Update Read Model
        await stockReadRepository.SetAvailableQuantityAsync(stock.Sku, stock.AvailableQuantity, cancellationToken);

        return (reservation.Id, true, "Stock reserved successfully.");
    }
}

public record ReleaseStockCommand(Guid ReservationId) : IRequest<bool>;

public class ReleaseStockCommandHandler(IInventoryDbContext context, IPublishEndpoint publishEndpoint, IStockReadRepository stockReadRepository) : IRequestHandler<ReleaseStockCommand, bool>
{
    public async Task<bool> Handle(ReleaseStockCommand request, CancellationToken cancellationToken)
    {
        var reservation = await context.Reservations.FirstOrDefaultAsync(r => r.Id == request.ReservationId, cancellationToken);
        if (reservation == null) return false;
        if (reservation.IsReleased) return true;

        reservation.Release();
        var stock = await context.Stocks.FirstOrDefaultAsync(s => s.Sku == reservation.Sku, cancellationToken);
        stock?.Release(reservation.Quantity);

        await publishEndpoint.Publish(new StockReleased(reservation.OrderId, reservation.Id, DateTimeOffset.UtcNow), cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        
        if (stock != null)
        {
            // Update Read Model
            await stockReadRepository.SetAvailableQuantityAsync(stock.Sku, stock.AvailableQuantity, cancellationToken);
        }

        return true;
    }
}

public record GetStockAvailabilityQuery(string Sku) : IRequest<int>;

public class GetStockAvailabilityQueryHandler(IStockReadRepository stockReadRepository) : IRequestHandler<GetStockAvailabilityQuery, int>
{
    public async Task<int> Handle(GetStockAvailabilityQuery request, CancellationToken cancellationToken)
    {
        // CQRS read-side isolation: queries never fall through to the write DbContext.
        return await stockReadRepository.GetAvailableQuantityAsync(request.Sku, cancellationToken) ?? 0;
    }
}

