using MediatR;
using Payment.Application.Common.Interfaces;

namespace Payment.Application.Payment.Queries;

public record GetPaymenttatusQuery(Guid OrderId) : IRequest<string?>;

public class GetPaymenttatusQueryHandler(IPaymentReadRepository readRepository) : IRequestHandler<GetPaymenttatusQuery, string?>
{
    public async Task<string?> Handle(GetPaymenttatusQuery request, CancellationToken cancellationToken)
    {
        return await readRepository.GetPaymenttatusAsync(request.OrderId, cancellationToken);
    }
}

