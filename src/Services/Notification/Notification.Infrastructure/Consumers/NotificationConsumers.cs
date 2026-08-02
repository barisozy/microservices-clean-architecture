using System.Diagnostics.Metrics;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.Extensions.Logging;
using Notification.Domain.Entities;
using Notification.Infrastructure.Data;

namespace Notification.Infrastructure.Consumers;

public class OrderShippedConsumer : IConsumer<OrderShipped>
{
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<OrderShippedConsumer> _logger;

    public OrderShippedConsumer(NotificationDbContext dbContext, ILogger<OrderShippedConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderShipped> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Notification.Api: Processing OrderShipped event for Order '{OrderId}', Tracking '{TrackingId}'", msg.OrderId, msg.TrackingId);

        _dbContext.Logs.Add(new NotificationLog
        {
            EventType = "OrderShipped",
            RecipientEmail = "customer@example.com",
            Subject = $"Order #{msg.OrderId} Has Been Shipped",
            Content = $"Your order has shipped. Tracking number: {msg.TrackingId}",
            SentAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        NotificationMetrics.DispatchCount.Add(1, new KeyValuePair<string, object?>("event.type", "OrderShipped"));
    }
}

public class PaymentFailedConsumer : IConsumer<PaymentFailed>
{
    private readonly NotificationDbContext _dbContext;
    private readonly ILogger<PaymentFailedConsumer> _logger;

    public PaymentFailedConsumer(NotificationDbContext dbContext, ILogger<PaymentFailedConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentFailed> context)
    {
        var msg = context.Message;
        _logger.LogInformation("Notification.Api: Processing PaymentFailed event for Order '{OrderId}', Reason '{Reason}'", msg.OrderId, msg.Reason);

        _dbContext.Logs.Add(new NotificationLog
        {
            EventType = "PaymentFailed",
            RecipientEmail = "customer@example.com",
            Subject = $"Payment Failed for Order #{msg.OrderId}",
            Content = $"Payment attempt failed. Reason: {msg.Reason}",
            SentAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        NotificationMetrics.DispatchCount.Add(1, new KeyValuePair<string, object?>("event.type", "PaymentFailed"));
    }
}

internal static class NotificationMetrics
{
    private static readonly Meter Meter = new("Notification.Api");
    internal static readonly Counter<long> DispatchCount =
        Meter.CreateCounter<long>("notification.dispatch.count");
}
