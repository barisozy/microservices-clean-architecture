using ECommerce.Contracts.Events.v1;
using Fulfillment.Application.Common.Interfaces;
using Fulfillment.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fulfillment.Application.Consumers;

public class PaymentCompletedConsumer(
    IFulfillmentDbContext context,
    IPublishEndpoint publishEndpoint,
    ILogger<PaymentCompletedConsumer> logger,
    IFulfillmentReadRepository readRepository) : IConsumer<PaymentCompleted>
{
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

        context.Tasks.Add(task);
        await context.SaveChangesAsync(contextEvent.CancellationToken);

        await readRepository.SetFulfillmentStatusAsync(message.OrderId, task.Status, contextEvent.CancellationToken);

        logger.LogInformation("Order {OrderId} mock shipped. Tracking Number: {TrackingNumber}", message.OrderId, task.TrackingNumber);

        await publishEndpoint.Publish(new OrderShipped(message.OrderId, task.TrackingNumber, DateTimeOffset.UtcNow), contextEvent.CancellationToken);
    }
}
