using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Payment.Application.Consumers;

public class RefundPaymentConsumer(ILogger<RefundPaymentConsumer> logger) : IConsumer<RefundPayment>
{
    public Task Consume(ConsumeContext<RefundPayment> context)
    {
        logger.LogInformation("Payment refund processed for OrderId: {OrderId} Reason: {Reason}", context.Message.OrderId, context.Message.Reason);
        return Task.CompletedTask;
    }
}