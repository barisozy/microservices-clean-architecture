using ECommerce.Contracts.Events;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Inventory.Commands;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Consumers;

public class OrderCancelledConsumer(ISender sender, IInventoryDbContext dbContext) : IConsumer<OrderCancelledEvent>
{
    public async Task Consume(ConsumeContext<OrderCancelledEvent> context)
    {
        var reservation = await dbContext.Reservations.FirstOrDefaultAsync(r => r.OrderId == context.Message.OrderId, context.CancellationToken);
        if (reservation != null)
        {
            await sender.Send(new ReleaseStockCommand(reservation.Id), context.CancellationToken);
        }
    }
}

