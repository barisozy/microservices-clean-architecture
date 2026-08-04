using MediatR;
using Order.Application.Common.Interfaces;

namespace Order.Application.Orders.Commands;

public record MarkOrderAsPaidCommand(Guid OrderId) : IRequest;

public class MarkOrderAsPaidCommandHandler(IOrderWriteRepository context) : IRequestHandler<MarkOrderAsPaidCommand>
{
    public async Task Handle(MarkOrderAsPaidCommand request, CancellationToken cancellationToken)
    {
        var order = await context.FindByIdAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            throw new InvalidOperationException($"Order {request.OrderId} was not found.");
        }

        order.MarkAsPaid();
        await context.SaveChangesAsync(cancellationToken);
    }
}
