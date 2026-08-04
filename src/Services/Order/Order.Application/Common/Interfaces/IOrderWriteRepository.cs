using Order.Domain.Entities;
using Order.Domain.Enums;

namespace Order.Application.Common.Interfaces;

public interface IOrderWriteRepository
{
    Task<global::Order.Domain.Entities.Order?> FindByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<global::Order.Domain.Entities.Order?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);
    Task<OrderStatus?> GetStatusAsync(Guid orderId, CancellationToken cancellationToken = default);
    void Add(global::Order.Domain.Entities.Order order);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
