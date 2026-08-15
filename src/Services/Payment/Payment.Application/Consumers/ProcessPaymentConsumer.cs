using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Payment.Application.Payment.Commands;
using Microsoft.Extensions.Logging;

namespace Payment.Application.Consumers;

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
