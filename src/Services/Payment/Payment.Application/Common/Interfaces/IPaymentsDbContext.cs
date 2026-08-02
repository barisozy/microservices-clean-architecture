using Microsoft.EntityFrameworkCore;
using Payment.Domain.Entities;

namespace Payment.Application.Common.Interfaces;

public interface IUser
{
    string? Id { get; }
}

public interface IPaymentDbContext
{
    DbSet<PaymentRecord> Payment { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

