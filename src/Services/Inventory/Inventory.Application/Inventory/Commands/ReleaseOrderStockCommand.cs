using Inventory.Application.Common.Interfaces;
using MediatR;
using Inventory.Domain.Entities;

namespace Inventory.Application.Inventory.Commands;

public record ReleaseOrderStockCommand(Guid OrderId) : IRequest;

public sealed class ReleaseOrderStockCommandHandler(
    IInventoryWriteRepository context,
    ISender sender)
    : IRequestHandler<ReleaseOrderStockCommand>
{
    public async Task Handle(
        ReleaseOrderStockCommand request,
        CancellationToken cancellationToken)
    {
        var reservation = await context.FindReservationByOrderIdAsync(request.OrderId, cancellationToken);
        if (reservation is not null && !reservation.IsTerminal)
        {
            await sender.Send(new ReleaseStockCommand(reservation.Id), cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
