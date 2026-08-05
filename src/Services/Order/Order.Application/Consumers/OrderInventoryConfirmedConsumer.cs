using ECommerce.Contracts.Events.v1;
using MassTransit;
using Order.Application.Common.Interfaces;

namespace Order.Application.Consumers;

public sealed class OrderInventoryConfirmedConsumer(IOrderWriteRepository db) : IConsumer<OrderInventoryConfirmed>
{
    public async Task Consume(ConsumeContext<OrderInventoryConfirmed> context)
    {
        var order = await db.FindByIdAsync(context.Message.OrderId, context.CancellationToken);

        if (order is null)
            throw new InvalidOperationException($"Order {context.Message.OrderId} was not found for inventory confirmation.");

        order.ConfirmInventory();
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
