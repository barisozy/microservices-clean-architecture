using Microsoft.EntityFrameworkCore;
using Payments.Domain.Entities;

namespace Payments.Application.Common.Interfaces;

public interface IUser
{
    string? Id { get; }
}

public interface IPaymentsDbContext
{
    DbSet<PaymentRecord> Payments { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
