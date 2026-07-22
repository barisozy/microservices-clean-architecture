using ECommerce.Contracts.Events;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Common.Interfaces;

namespace Ordering.Application.Orders.Commands.CancelOrder;

public record CancelOrderCommand(Guid OrderId, string Reason) : IRequest<bool>;

public class CancelOrderCommandHandler(IOrderingDbContext context, IPublishEndpoint publishEndpoint) : IRequestHandler<CancelOrderCommand, bool>
{
    public async Task<bool> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await context.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);
        if (order == null) return false;

        order.Cancel(request.Reason);   // Sets Status = Cancelled (idempotent if already cancelled)
        await context.SaveChangesAsync(cancellationToken);

        // OrderCancelledEvent is published via MassTransit Outbox — consumed by Inventory.Api to release stock
        await publishEndpoint.Publish(new OrderCancelledEvent(request.OrderId, request.Reason, DateTimeOffset.UtcNow), cancellationToken);
        return true;
    }
}

