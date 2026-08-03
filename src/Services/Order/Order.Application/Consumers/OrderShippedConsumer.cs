using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Order.Application.Orders.Commands;

namespace Order.Application.Consumers;

public class OrderShippedConsumer(
    ISender sender,
    ILogger<OrderShippedConsumer> logger) : IConsumer<OrderShipped>
{
    public async Task Consume(ConsumeContext<OrderShipped> context)
    {
        var message = context.Message;
        logger.LogInformation("Processing OrderShipped for Order {OrderId}", message.OrderId);

        await sender.Send(new MarkOrderAsShippedCommand(message.OrderId), context.CancellationToken);
    }
}
