using MediatR;
using Payments.Application.Common.Interfaces;

namespace Payments.Application.Payments.Queries;

public record GetPaymentStatusQuery(Guid OrderId) : IRequest<string?>;

public class GetPaymentStatusQueryHandler(IPaymentReadRepository readRepository) : IRequestHandler<GetPaymentStatusQuery, string?>
{
    public async Task<string?> Handle(GetPaymentStatusQuery request, CancellationToken cancellationToken)
    {
        return await readRepository.GetPaymentStatusAsync(request.OrderId, cancellationToken);
    }
}
