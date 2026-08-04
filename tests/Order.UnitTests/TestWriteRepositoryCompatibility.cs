using Microsoft.EntityFrameworkCore;
using Order.Application.Common.Interfaces;
using Order.Domain.Enums;

namespace Order.Application.Common.Interfaces;

// Compatibility adapter for legacy unit tests. Production code uses IOrderWriteRepository.
public interface IOrderDbContext : IOrderWriteRepository
{
    DbSet<global::Order.Domain.Entities.Order> Orders { get; }
    new Task<global::Order.Domain.Entities.Order?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) => Orders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    new Task<global::Order.Domain.Entities.Order?> FindByIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default) => Orders.FirstOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);
    new Task<OrderStatus?> GetStatusAsync(Guid id, CancellationToken cancellationToken = default) => Orders.AsNoTracking().Where(x => x.Id == id).Select(x => (OrderStatus?)x.Status).SingleOrDefaultAsync(cancellationToken);
    new void Add(global::Order.Domain.Entities.Order order) => Orders.Add(order);
}
