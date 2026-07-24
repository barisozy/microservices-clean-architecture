using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Payments.Application.Payments.Commands;
using Microsoft.Extensions.Logging;

namespace Payments.Application.Consumers;

public class StockReservedConsumer(ISender sender, ILogger<StockReservedConsumer> logger) : IConsumer<StockReserved>
{
    public async Task Consume(ConsumeContext<StockReserved> context)
    {
        var msg = context.Message;
        logger.LogInformation("Stock reserved for OrderId: {OrderId}. Proceeding to Payment...", msg.OrderId);
        
        // Pass items from StockReserved to handle specific logic like FAIL_PAYMENT
        await sender.Send(new ProcessPaymentCommand(msg.OrderId, msg.IdempotencyKey, msg.TotalAmount, msg.Items, msg.OrderCreatedAt), context.CancellationToken);
    }
}
