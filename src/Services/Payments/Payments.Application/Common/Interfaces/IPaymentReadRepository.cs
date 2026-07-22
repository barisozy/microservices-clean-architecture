namespace Payments.Application.Common.Interfaces;

public interface IPaymentReadRepository
{
    Task<string?> GetPaymentStatusAsync(Guid orderId, CancellationToken cancellationToken);
    Task SetPaymentStatusAsync(Guid orderId, string status, CancellationToken cancellationToken);
}
