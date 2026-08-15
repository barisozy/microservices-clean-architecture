using Microsoft.EntityFrameworkCore;
using Order.Application.Common.Interfaces;
using Order.Domain.Enums;

namespace Order.Infrastructure.Data.Repositories;

internal sealed class OrderWriteRepository : IOrderWriteRepository
{
    private readonly OrderDbContext _dbContext;

    public OrderWriteRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<global::Order.Domain.Entities.Order?> FindByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
    }

    public Task<global::Order.Domain.Entities.Order?> FindByIdempotencyKeyAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        return _dbContext.Orders.FirstOrDefaultAsync(x => x.CustomerId == customerId && x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<OrderStatus?> GetStatusAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Where(x => x.Id == orderId)
            .Select(x => new { x.Status })
            .FirstOrDefaultAsync(cancellationToken);
            
        return order?.Status;
    }

    public void Add(global::Order.Domain.Entities.Order order)
    {
        _dbContext.Orders.Add(order);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
