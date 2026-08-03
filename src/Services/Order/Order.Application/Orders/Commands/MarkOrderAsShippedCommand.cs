using MediatR;
using Microsoft.EntityFrameworkCore;
using Order.Application.Common.Interfaces;

namespace Order.Application.Orders.Commands;

public record MarkOrderAsShippedCommand(Guid OrderId) : IRequest;

public class MarkOrderAsShippedCommandHandler(IOrderDbContext context) : IRequestHandler<MarkOrderAsShippedCommand>
{
    public async Task Handle(MarkOrderAsShippedCommand request, CancellationToken cancellationToken)
    {
        var order = await context.Orders.FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            throw new InvalidOperationException($"Order {request.OrderId} was not found.");
        }

        order.MarkAsShipped();
        await context.SaveChangesAsync(cancellationToken);
    }
}
