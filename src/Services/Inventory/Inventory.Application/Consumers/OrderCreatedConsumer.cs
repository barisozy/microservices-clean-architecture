using ECommerce.Contracts.Events.v1;
using Inventory.Application.Common.Interfaces;
using Inventory.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Inventory.Application.Inventory.Commands;

namespace Inventory.Application.Consumers;

public class OrderCreatedConsumer(ISender sender, IPublishEndpoint publishEndpoint, ILogger<OrderCreatedConsumer> logger) : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        var msg = context.Message;
        logger.LogInformation("Processing stock reservation for OrderId: {OrderId}", msg.OrderId);

        // For simplicity in template, take the first item
        var sku = msg.Items.FirstOrDefault()?.Sku ?? "UNKNOWN_SKU";
        var quantity = msg.Items.Sum(x => x.Quantity);

        var (reservationId, success, message) = await sender.Send(new ReserveStockCommand(msg.OrderId, sku, quantity), context.CancellationToken);

        if (success)
        {
            logger.LogInformation("Stock reserved for OrderId: {OrderId}. Publishing StockReserved...", msg.OrderId);

            // Continue the Saga: Proceed to Payment
            await publishEndpoint.Publish(new StockReserved(
                msg.OrderId,
                msg.CustomerId,
                msg.IdempotencyKey,
                msg.Items,
                msg.TotalAmount,
                DateTimeOffset.UtcNow
            ), context.CancellationToken);
        }
        else
        {
            logger.LogWarning("Failed to reserve stock for OrderId: {OrderId}. Reason: {Message}", msg.OrderId, message);
            // In a full saga, we would publish StockReservationFailedEvent here to rollback the order.
            // But since the order was just created and not paid, we could just cancel it.
            await publishEndpoint.Publish(new OrderCancelled(msg.OrderId, "Stock Reservation Failed", DateTimeOffset.UtcNow), context.CancellationToken);
        }
    }
}
