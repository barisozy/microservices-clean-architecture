using FluentValidation;
using Fulfillment.Application.Common.Interfaces;
using MediatR;

namespace Fulfillment.Application.Shipments;

public sealed record GetShipmentQuery(Guid OrderId) : IRequest<ShipmentReadModel?>;

public sealed class GetShipmentQueryValidator : AbstractValidator<GetShipmentQuery>
{
    public GetShipmentQueryValidator() => RuleFor(request => request.OrderId).NotEmpty();
}

public sealed class GetShipmentQueryHandler(IFulfillmentReadRepository readRepository)
    : IRequestHandler<GetShipmentQuery, ShipmentReadModel?>
{
    public async Task<ShipmentReadModel?> Handle(GetShipmentQuery request, CancellationToken cancellationToken) =>
        await readRepository.GetShipmentAsync(request.OrderId, cancellationToken);
}
