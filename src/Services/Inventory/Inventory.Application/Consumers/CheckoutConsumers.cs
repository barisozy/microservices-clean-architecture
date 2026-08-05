using ECommerce.Contracts.Events.v1;
using Inventory.Application.Common.Interfaces;
using Inventory.Domain.Entities;
using Inventory.Application.Inventory.Commands;
using MassTransit;
using MediatR;
using System.Linq;

namespace Inventory.Application.Consumers;

public sealed class ReserveInventoryConsumer(
    IInventoryWriteRepository db,
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

        var itemsDict = request.Items.ToDictionary(i => i.Sku, i => i.Quantity);
        var fingerprint = InventoryReservation.GenerateFingerprint(request.OrderId, itemsDict);
        var now = leasePolicy.UtcNow;
        
        var existing = await db.FindReservationByOrderIdAsync(request.OrderId, context.CancellationToken);
        if (existing is not null)
        {
            if (existing.RequestFingerprint != fingerprint)
            {
                await Reject(context, "RESERVATION_FINGERPRINT_MISMATCH");
                return;
            }

            if (existing.Status is InventoryReservationStatus.Released or InventoryReservationStatus.Expired)
            {
                await Reject(context, "RESERVATION_NOT_ACTIVE");
                return;
            }

            if (existing.Status == InventoryReservationStatus.Pending && existing.ExpiresAt <= now)
            {
                await Reject(context, "RESERVATION_EXPIRED");
                return;
            }

            await publishEndpoint.Publish(new InventoryReserved(request.OrderId, existing.Id, existing.ExpiresAt), publishContext => publishContext.CorrelationId = request.OrderId, context.CancellationToken);
            await db.SaveChangesAsync(context.CancellationToken);
            return;
        }

        foreach (var item in request.Items)
        {
            var stock = await db.FindStockAsync(item.Sku, context.CancellationToken);
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
            stock.Reserve(item.Quantity);
        }

        var expiresAt = leasePolicy.GetExpiry(now);
        var reservation = InventoryReservation.Create(request.OrderId, itemsDict, expiresAt);
        db.Add(reservation);

        await publishEndpoint.Publish(new InventoryReserved(request.OrderId, reservation.Id, expiresAt), publishContext => publishContext.CorrelationId = request.OrderId, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    private async Task Reject(ConsumeContext<ReserveInventory> context, string reason)
    {
        await publishEndpoint.Publish(new InventoryReservationRejected(context.Message.OrderId, reason), publishContext => publishContext.CorrelationId = context.Message.OrderId, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

public sealed class CommitInventoryReservationConsumer(
    IInventoryWriteRepository db,
    IPublishEndpoint publishEndpoint,
    IInventoryReservationLeasePolicy leasePolicy) : IConsumer<CommitInventoryReservation>
{
    public async Task Consume(ConsumeContext<CommitInventoryReservation> context)
    {
        var reservation = await db.FindReservationByOrderIdAsync(context.Message.OrderId, context.CancellationToken);
        if (reservation is null)
        {
            await Reject(context, "RESERVATION_NOT_FOUND");
            return;
        }

        var now = leasePolicy.UtcNow;
        if (reservation.Status == InventoryReservationStatus.Committed)
        {
            await PublishCommitted(context, reservation.Id);
            return;
        }

        if (reservation.Status == InventoryReservationStatus.Expired ||
            (reservation.Status == InventoryReservationStatus.Pending && reservation.ExpiresAt <= now))
        {
            await Reject(context, "RESERVATION_EXPIRED");
            return;
        }

        if (reservation.Status != InventoryReservationStatus.Pending)
        {
            await Reject(context, "INVALID_RESERVATION_STATE");
            return;
        }

        if (!reservation.Commit(now))
        {
            await Reject(context, "RESERVATION_EXPIRED");
            return;
        }

        await PublishCommitted(context, reservation.Id);
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
