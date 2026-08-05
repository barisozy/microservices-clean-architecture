using Payment.Domain.Entities;

namespace Payment.Application.Common.Interfaces;

public interface IPaymentWriteRepository
{
    Task<PaymentRecord?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    void Add(PaymentRecord payment);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
