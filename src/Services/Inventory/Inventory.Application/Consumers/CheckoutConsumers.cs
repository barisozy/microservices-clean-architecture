using ECommerce.Contracts.Events.v1;
using Inventory.Application.Common.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Application.Inventory.Commands;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Consumers;

public sealed class ReserveInventoryConsumer(
    IInventoryDbContext db,
    IPublishEndpoint publishEndpoint,
    IInventoryReservationLeasePolicy leasePolicy) : IConsumer<ReserveInventory>
{
    public async Task Consume(ConsumeContext<ReserveInventory> context)
    {
        var request = context.Message;
        if (request.OrderId == Guid.Empty || request.Items is null || request.Items.Count is 0)
        {
            await Reject(context, "INVALID_RESERVATION_REQUEST");
            return;
        }

        if (request.Items.Count > 100 ||
            request.Items.Any(item => string.IsNullOrWhiteSpace(item.Sku) || item.Quantity <= 0) ||
            request.Items.GroupBy(item => item.Sku, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            await Reject(context, "INVALID_RESERVATION_REQUEST");
            return;
        }

        var now = leasePolicy.UtcNow;
        var existing = await db.Reservations
            .Where(x => x.OrderId == request.OrderId)
            .ToListAsync(context.CancellationToken);
        if (existing.Count > 0)
        {
            if (existing.Any(x => x.Status is InventoryReservationStatus.Released or InventoryReservationStatus.Expired))
            {
                await Reject(context, "RESERVATION_NOT_ACTIVE");
                return;
            }

            if (existing.Any(x => x.Status == InventoryReservationStatus.Pending && x.ExpiresAt <= now))
            {
                await Reject(context, "RESERVATION_EXPIRED");
                return;
            }

            var expiry = existing.Min(x => x.ExpiresAt);
            await publishEndpoint.Publish(new InventoryReserved(request.OrderId, existing[0].Id, expiry), publishContext => publishContext.CorrelationId = request.OrderId, context.CancellationToken);
            await db.SaveChangesAsync(context.CancellationToken);
            return;
        }

        var stocks = new List<(Stock Stock, ECommerce.Contracts.Events.v1.OrderItemContractDto Item)>();
        foreach (var item in request.Items)
        {
            var stock = await db.Stocks.FirstOrDefaultAsync(x => x.Sku == item.Sku, context.CancellationToken);
            if (stock is null)
            {
                await Reject(context, "UNKNOWN_SKU");
                return;
            }
            if (stock.AvailableQuantity < item.Quantity)
            {
                await Reject(context, "INSUFFICIENT_STOCK");
                return;
            }
            stocks.Add((stock, item));
        }

        var expiresAt = leasePolicy.GetExpiry(now);
        var reservations = stocks.Select(x =>
        {
            x.Stock.Reserve(x.Item.Quantity);
            return InventoryReservation.Create(request.OrderId, x.Item.Sku, x.Item.Quantity, expiresAt);
        }).ToList();
        foreach (var reservation in reservations)
            db.Reservations.Add(reservation);

        await publishEndpoint.Publish(new InventoryReserved(request.OrderId, reservations[0].Id, expiresAt), publishContext => publishContext.CorrelationId = request.OrderId, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    private async Task Reject(ConsumeContext<ReserveInventory> context, string reason)
    {
        await publishEndpoint.Publish(new InventoryReservationRejected(context.Message.OrderId, reason), publishContext => publishContext.CorrelationId = context.Message.OrderId, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

public sealed class CommitInventoryReservationConsumer(
    IInventoryDbContext db,
    IPublishEndpoint publishEndpoint,
    IInventoryReservationLeasePolicy leasePolicy) : IConsumer<CommitInventoryReservation>
{
    public async Task Consume(ConsumeContext<CommitInventoryReservation> context)
    {
        var reservations = await db.Reservations.Where(x => x.OrderId == context.Message.OrderId).ToListAsync(context.CancellationToken);
        if (reservations.Count == 0)
        {
            await Reject(context, "RESERVATION_NOT_FOUND");
            return;
        }

        var now = leasePolicy.UtcNow;
        if (reservations.All(x => x.Status == InventoryReservationStatus.Committed))
        {
            await PublishCommitted(context, reservations[0].Id);
            return;
        }

        if (reservations.Any(x => x.Status == InventoryReservationStatus.Expired ||
                                  (x.Status == InventoryReservationStatus.Pending && x.ExpiresAt <= now)))
        {
            await Reject(context, "RESERVATION_EXPIRED");
            return;
        }

        if (reservations.Any(x => x.Status != InventoryReservationStatus.Pending))
        {
            await Reject(context, "INVALID_RESERVATION_STATE");
            return;
        }

        foreach (var reservation in reservations)
        {
            if (!reservation.Commit(now))
            {
                await Reject(context, "RESERVATION_EXPIRED");
                return;
            }
        }

        await PublishCommitted(context, reservations[0].Id);
    }

    private async Task PublishCommitted(ConsumeContext<CommitInventoryReservation> context, Guid reservationId)
    {
        await publishEndpoint.Publish(new InventoryReservationCommitted(context.Message.OrderId, reservationId), publishContext => publishContext.CorrelationId = context.Message.OrderId, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    private async Task Reject(ConsumeContext<CommitInventoryReservation> context, string reason)
    {
        await publishEndpoint.Publish(new InventoryReservationRejected(context.Message.OrderId, reason), publishContext => publishContext.CorrelationId = context.Message.OrderId, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

public sealed class ReleaseInventoryReservationConsumer(ISender sender) : IConsumer<ReleaseInventoryReservation>
{
    public async Task Consume(ConsumeContext<ReleaseInventoryReservation> context) =>
        await sender.Send(new ReleaseOrderStockCommand(context.Message.OrderId), context.CancellationToken);
}
