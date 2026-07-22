using Ordering.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Ordering.Infrastructure.Services;

public class ValkeyBasketService : IBasketService
{
    private readonly IConnectionMultiplexer _valkey;

    public ValkeyBasketService(IConnectionMultiplexer valkey)
    {
        _valkey = valkey;
    }

    public async Task<bool> DeleteBasketAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var db = _valkey.GetDatabase();
        return await db.KeyDeleteAsync(buyerId);
    }
}
