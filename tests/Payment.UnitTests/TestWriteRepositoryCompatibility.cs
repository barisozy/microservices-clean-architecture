using Microsoft.EntityFrameworkCore;
using Payment.Application.Common.Interfaces;
using Payment.Domain.Entities;

namespace Payment.Application.Common.Interfaces;

// Compatibility adapter for legacy unit tests. Production code uses IPaymentWriteRepository.
public interface IPaymentDbContext : IPaymentWriteRepository
{
    DbSet<PaymentRecord> Payment { get; }
    new Task<PaymentRecord?> FindByIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default) => Payment.FirstOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);
    new void Add(PaymentRecord payment) => Payment.Add(payment);
}
