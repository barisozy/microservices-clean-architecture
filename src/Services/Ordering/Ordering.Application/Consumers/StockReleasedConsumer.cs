using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Ordering.Application.Orders.Commands.CancelOrder;
using Microsoft.Extensions.Logging;

namespace Ordering.Application.Consumers;

public class StockReleasedConsumer(ISender sender, ILogger<StockReleasedConsumer> logger) : IConsumer<StockReleased>
{
    public async Task Consume(ConsumeContext<StockReleased> context)
    {
        var msg = context.Message;
        logger.LogWarning("Stock released for ReservationId: {ReservationId}. Cancelling Order (Rollback Complete)...", msg.ReservationId);

        // Since StockReleased does not carry OrderId natively, we either need to add OrderId to StockReleased
        // or we handle this mapping. Let's assume we can add OrderId to StockReleased.
        // Wait, I will pass OrderId in StockReleased. I will update it.
        await sender.Send(new CancelOrderCommand(msg.OrderId, "Saga Rollback: Stock Released (Payment Failed)"), context.CancellationToken);
    }
}
