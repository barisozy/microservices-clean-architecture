namespace Payment.Application.Common.Interfaces;

public interface IPaymentReadRepository
{
    Task<string?> GetPaymenttatusAsync(Guid orderId, CancellationToken cancellationToken);
    Task SetPaymenttatusAsync(Guid orderId, string status, CancellationToken cancellationToken);
}

