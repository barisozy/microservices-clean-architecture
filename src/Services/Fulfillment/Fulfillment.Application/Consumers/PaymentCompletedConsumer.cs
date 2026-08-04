using System.Diagnostics.Metrics;
using ECommerce.Contracts.Events.v1;
using Fulfillment.Application.Common.Interfaces;
using Fulfillment.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fulfillment.Application.Consumers;

public class PaymentCompletedConsumer(
    IFulfillmentWriteRepository context,
    IPublishEndpoint publishEndpoint,
    ILogger<PaymentCompletedConsumer> logger,
    IFulfillmentReadRepository readRepository) : IConsumer<PaymentCompleted>
{
    private static readonly Meter _meter = new("Fulfillment.Api", "1.0.0");
    private static readonly Histogram<double> _createToShipDuration = _meter.CreateHistogram<double>(
        "order.create_to_ship.duration", 
        unit: "s", 
        description: "Time from order creation to shipment");

    public async Task Consume(ConsumeContext<PaymentCompleted> contextEvent)
    {
        var message = contextEvent.Message;
        logger.LogInformation("Processing PaymentCompleted for Order {OrderId}", message.OrderId);

        var task = new FulfillmentTask
        {
            OrderId = message.OrderId,
            Status = "Shipped",
            TrackingNumber = $"TRACK-{Guid.NewGuid().ToString()[..8].ToUpper()}"
        };

        context.Add(task);

        var existingShipment = await context.FindShipmentAsync(message.OrderId, contextEvent.CancellationToken);
        if (existingShipment == null)
        {
            context.Add(new Shipment
            {
                OrderId = message.OrderId,
                TrackingNumber = task.TrackingNumber,
                Status = "SHIPPED",
                ShippedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync(contextEvent.CancellationToken);
        await publishEndpoint.Publish(
            new OrderShipped(message.OrderId, task.TrackingNumber, DateTimeOffset.UtcNow),
            contextEvent.CancellationToken);

        await readRepository.SetShipmentAsync(
            new ShipmentReadModel(message.OrderId, task.TrackingNumber, task.Status, DateTime.UtcNow),
            contextEvent.CancellationToken);
        await readRepository.SetFulfillmentStatusAsync(message.OrderId, task.Status, contextEvent.CancellationToken);

        logger.LogInformation("Order {OrderId} mock shipped. Tracking Number: {TrackingNumber}", message.OrderId, task.TrackingNumber);

        if (message.OrderCreatedAt != default)
        {
            var duration = (DateTimeOffset.UtcNow - message.OrderCreatedAt).TotalSeconds;
            _createToShipDuration.Record(duration);
            logger.LogInformation("Recorded order.create_to_ship.duration: {Duration}s", duration);
        }
    }
}
