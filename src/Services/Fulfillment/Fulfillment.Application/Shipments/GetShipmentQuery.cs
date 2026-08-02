using FluentValidation;
using Fulfillment.Application.Common.Interfaces;
using Fulfillment.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Fulfillment.Application.Shipments;

public sealed record GetShipmentQuery(Guid OrderId) : IRequest<Shipment?>;

public sealed class GetShipmentQueryValidator : AbstractValidator<GetShipmentQuery>
{
    public GetShipmentQueryValidator() => RuleFor(request => request.OrderId).NotEmpty();
}

public sealed class GetShipmentQueryHandler(IFulfillmentDbContext dbContext)
    : IRequestHandler<GetShipmentQuery, Shipment?>
{
    public Task<Shipment?> Handle(GetShipmentQuery request, CancellationToken cancellationToken) =>
        dbContext.Shipments.AsNoTracking()
            .FirstOrDefaultAsync(shipment => shipment.OrderId == request.OrderId, cancellationToken);
}
