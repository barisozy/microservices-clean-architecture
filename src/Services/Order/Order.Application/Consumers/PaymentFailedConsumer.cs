using System.Diagnostics.Metrics;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.Extensions.Logging;
using Order.Application.Common.Interfaces;
using Order.Domain.Enums;

namespace Order.Application.Consumers;

public class PaymentFailedConsumer(
    IOrderWriteRepository dbContext,
    IPublishEndpoint publishEndpoint,
    ILogger<PaymentFailedConsumer> logger) : IConsumer<PaymentFailed>
{
    private static readonly Meter Meter = new("Order.Api");
    private static readonly Counter<long> CompensationCount =
        Meter.CreateCounter<long>("saga.compensation.count");

    public async Task Consume(ConsumeContext<PaymentFailed> context)
    {
        var message = context.Message;
        logger.LogWarning("Processing PaymentFailed event for OrderId {OrderId}, Reason: {Reason}",
            message.OrderId, message.Reason);

        var order = await dbContext.FindByIdAsync(message.OrderId, context.CancellationToken);

        if (order == null)
        {
            logger.LogWarning("Order {OrderId} not found for PaymentFailed compensation", message.OrderId);
            return;
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            return;
        }

        order.Cancel($"Payment failed: {message.Reason}");
        await dbContext.SaveChangesAsync(context.CancellationToken);
        await publishEndpoint.Publish(new OrderCancelled(
            message.OrderId,
            message.Reason,
            DateTimeOffset.UtcNow), context.CancellationToken);
        CompensationCount.Add(1);

        logger.LogInformation("Order {OrderId} cancelled and OrderCancelled event published", message.OrderId);
    }
}
