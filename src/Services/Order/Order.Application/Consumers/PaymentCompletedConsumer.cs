using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Order.Application.Orders.Commands;

namespace Order.Application.Consumers;

public class PaymentCompletedConsumer(
    ISender sender,
    ILogger<PaymentCompletedConsumer> logger) : IConsumer<PaymentCompleted>
{
    public async Task Consume(ConsumeContext<PaymentCompleted> context)
    {
        var message = context.Message;
        logger.LogInformation("Processing PaymentCompleted for Order {OrderId}", message.OrderId);

        await sender.Send(new MarkOrderAsPaidCommand(message.OrderId), context.CancellationToken);
    }
}
