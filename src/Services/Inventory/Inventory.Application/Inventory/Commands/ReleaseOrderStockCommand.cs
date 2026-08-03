using Inventory.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Inventory.Commands;

public record ReleaseOrderStockCommand(Guid OrderId) : IRequest;

public sealed class ReleaseOrderStockCommandHandler(
    IInventoryDbContext context,
    ISender sender)
    : IRequestHandler<ReleaseOrderStockCommand>
{
    public async Task Handle(
        ReleaseOrderStockCommand request,
        CancellationToken cancellationToken)
    {
        var reservationIds = await context.Reservations
            .Where(r =>
                r.OrderId == request.OrderId &&
                !r.IsReleased)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        foreach (var reservationId in reservationIds)
        {
            await sender.Send(
                new ReleaseStockCommand(reservationId),
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
