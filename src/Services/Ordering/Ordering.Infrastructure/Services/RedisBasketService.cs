using Ordering.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Ordering.Infrastructure.Services;

public class RedisBasketService : IBasketService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisBasketService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> DeleteBasketAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        return await db.KeyDeleteAsync(buyerId);
    }
}
