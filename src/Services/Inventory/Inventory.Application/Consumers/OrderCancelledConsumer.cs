using ECommerce.Contracts.Events.v1;
using Inventory.Application.Inventory.Commands;
using MassTransit;
using MediatR;

namespace Inventory.Application.Consumers;

public class OrderCancelledConsumer(ISender sender) : IConsumer<OrderCancelled>
{
    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        await sender.Send(new ReleaseOrderStockCommand(context.Message.OrderId), context.CancellationToken);
    }
}
