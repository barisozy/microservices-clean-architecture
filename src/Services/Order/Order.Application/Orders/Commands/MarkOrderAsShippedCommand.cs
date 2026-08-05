using MediatR;
using Order.Application.Common.Interfaces;
using Order.Domain.Enums;

namespace Order.Application.Orders.Commands;

public record MarkOrderAsShippedCommand(Guid OrderId) : IRequest;

public class MarkOrderAsShippedCommandHandler(IOrderWriteRepository context) : IRequestHandler<MarkOrderAsShippedCommand>
{
    public async Task Handle(MarkOrderAsShippedCommand request, CancellationToken cancellationToken)
    {
        Order.Domain.Entities.Order? order = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);

        // PaymentCompleted and OrderShipped are independent integration events.
        // The shipped event can legitimately arrive while the payment consumer
        // is still committing the Paid transition. Keep the message in-flight
        // until that transition is visible instead of losing it to a transient
        // state-machine violation.
        var initialStatus = await context.GetStatusAsync(request.OrderId, cancellationToken);

        if (initialStatus is null)
        {
            throw new InvalidOperationException($"Order {request.OrderId} was not found.");
        }

        while (DateTime.UtcNow < deadline)
        {
            var currentStatus = await context.GetStatusAsync(request.OrderId, cancellationToken);

            if (currentStatus is not OrderStatus.Pending and not OrderStatus.PendingInventory and not OrderStatus.AwaitingPayment)
                break;

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        // Reload a tracked instance after the visibility wait so the state
        // transition and domain-event dispatch are persisted normally.
        order = await context.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            throw new InvalidOperationException($"Order {request.OrderId} was not found.");
        }

        // OrderShipped is emitted only by Fulfillment after PaymentCompleted.
        // If the completion consumer is still in-flight (or its delivery was
        // reordered), this event is sufficient evidence to reconcile the
        // aggregate before applying the final transition.
        if (order.Status is OrderStatus.Pending or OrderStatus.PendingInventory or OrderStatus.AwaitingPayment)
        {
            order.MarkAsPaid();
        }

        order.MarkAsShipped();
        await context.SaveChangesAsync(cancellationToken);
    }
}
