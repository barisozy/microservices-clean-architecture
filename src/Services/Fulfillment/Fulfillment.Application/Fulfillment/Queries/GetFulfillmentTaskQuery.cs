using Fulfillment.Application.Common.Interfaces;
using MediatR;

namespace Fulfillment.Application.Fulfillment.Queries;

public record GetFulfillmentTaskQuery(Guid OrderId) : IRequest<string?>;

public class GetFulfillmentTaskQueryHandler(IFulfillmentReadRepository readRepository) : IRequestHandler<GetFulfillmentTaskQuery, string?>
{
    public async Task<string?> Handle(GetFulfillmentTaskQuery request, CancellationToken cancellationToken)
    {
        return await readRepository.GetFulfillmentStatusAsync(request.OrderId, cancellationToken);
    }
}
