using Microsoft.EntityFrameworkCore;
using Order.Domain.Entities;

namespace Order.Application.Common.Interfaces;

public interface IOrderDbContext
{
    DbSet<global::Order.Domain.Entities.Order> Orders { get; }
    Task<global::Order.Domain.Entities.Order?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
