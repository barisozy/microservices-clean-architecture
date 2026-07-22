using ECommerce.Contracts.Events.v1;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Inventory.Commands;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.Consumers;

public class PaymentFailedConsumer(ISender sender, IInventoryDbContext dbContext, ILogger<PaymentFailedConsumer> logger) : IConsumer<PaymentFailed>
{
    public async Task Consume(ConsumeContext<PaymentFailed> context)
    {
        logger.LogWarning("Payment failed for OrderId: {OrderId}. Initiating Stock Rollback (Compensating Transaction)...", context.Message.OrderId);

        var reservation = await dbContext.Reservations.FirstOrDefaultAsync(r => r.OrderId == context.Message.OrderId, context.CancellationToken);
        if (reservation != null)
        {
            await sender.Send(new ReleaseStockCommand(reservation.Id), context.CancellationToken);
            logger.LogInformation("Stock released for OrderId: {OrderId} due to payment failure.", context.Message.OrderId);
        }
    }
}
