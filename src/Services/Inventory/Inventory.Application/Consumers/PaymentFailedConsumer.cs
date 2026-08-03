using ECommerce.Contracts.Events.v1;
using Inventory.Application.Inventory.Commands;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.Consumers;

public class PaymentFailedConsumer(ISender sender, ILogger<PaymentFailedConsumer> logger) : IConsumer<PaymentFailed>
{
    public async Task Consume(ConsumeContext<PaymentFailed> context)
    {
        await sender.Send(new ReleaseOrderStockCommand(context.Message.OrderId), context.CancellationToken);
        logger.LogInformation("Stock released for OrderId: {OrderId} due to payment failure.", context.Message.OrderId);
    }
}
