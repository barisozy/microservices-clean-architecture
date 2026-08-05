using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Payment.Application.Payment.Commands;
using Microsoft.Extensions.Logging;

namespace Payment.Application.Consumers;

/// <summary>
/// Sprint 1 plan: Payment.Api consumes OrderCreated directly (after stock is reserved sync via gRPC by Order.Api).
/// Async path: Order.Api Outbox → OrderCreated → Payment.Api (mock charge) → PaymentCompleted → Fulfillment.Api
/// </summary>
public class ProcessPaymentConsumer(ISender sender, ILogger<ProcessPaymentConsumer> logger) : IConsumer<ProcessPayment>
{
    public async Task Consume(ConsumeContext<ProcessPayment> context)
    {
        var msg = context.Message;
        logger.LogInformation("Payment processing for OrderId: {OrderId}", msg.OrderId);
        // We simulate payment completion right away for now
        await sender.Send(new ProcessPaymentCommand(msg.OrderId, msg.IdempotencyKey, msg.TotalAmount, msg.Items, DateTimeOffset.UtcNow), context.CancellationToken);
    }
}
