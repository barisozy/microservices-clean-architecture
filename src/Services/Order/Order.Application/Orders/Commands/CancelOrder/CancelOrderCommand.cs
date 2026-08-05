using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Order.Application.Common.Interfaces;

namespace Order.Application.Orders.Commands.CancelOrder;

public record CancelOrderCommand(Guid OrderId, string Reason) : IRequest<bool>;

public class CancelOrderCommandHandler(IOrderWriteRepository context, IPublishEndpoint publishEndpoint) : IRequestHandler<CancelOrderCommand, bool>
{
    public async Task<bool> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await context.FindByIdAsync(request.OrderId, cancellationToken);
        if (order == null) return false;

        order.Cancel(request.Reason);   // Sets Status = Cancelled (idempotent if already cancelled)
        
        // OrderCancelled is published via MassTransit Outbox — consumed by Inventory.Api to release stock
        await publishEndpoint.Publish(new OrderCancelled(request.OrderId, request.Reason, DateTimeOffset.UtcNow), cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
