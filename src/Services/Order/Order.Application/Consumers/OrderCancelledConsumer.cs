using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Order.Application.Common.Interfaces;

namespace Order.Application.Consumers;

public sealed class OrderCancelledConsumer(IOrderDbContext db) : IConsumer<OrderCancelled>
{
    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == context.Message.OrderId, context.CancellationToken);
        if (order is null || order.Status == Order.Domain.Enums.OrderStatus.Cancelled) return;
        order.Cancel(context.Message.Reason);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
