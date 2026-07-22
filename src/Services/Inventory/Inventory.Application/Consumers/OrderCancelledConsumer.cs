using ECommerce.Contracts.Events.v1;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Inventory.Commands;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Consumers;

public class OrderCancelledConsumer(ISender sender, IInventoryDbContext dbContext) : IConsumer<OrderCancelled>
{
    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        var reservation = await dbContext.Reservations.FirstOrDefaultAsync(r => r.OrderId == context.Message.OrderId, context.CancellationToken);
        if (reservation != null)
        {
            await sender.Send(new ReleaseStockCommand(reservation.Id), context.CancellationToken);
        }
    }
}

