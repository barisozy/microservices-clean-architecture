using Order.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Order.Infrastructure.Services;

/// <summary>
/// Redis Hash basket:{sub} (field=Sku, value=Qty) — sliding 7-day TTL refreshed on every write.
/// Plan Sprint 1, Task 19: basket:{KeycloakSubject} hash, sliding TTL.
/// </summary>
public class ValkeyBasketService(IConnectionMultiplexer valkey) : IBasketService
{
    private static string BasketKey(string buyerId) => $"basket:{buyerId}";
    private static readonly TimeSpan SlidingTtl = TimeSpan.FromDays(7);

    public async Task<Dictionary<string, int>> GetBasketAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var db = valkey.GetDatabase();
        var entries = await db.HashGetAllAsync(BasketKey(buyerId));
        return entries.ToDictionary(e => e.Name.ToString(), e => (int)e.Value);
    }

    public async Task<bool> SetBasketAsync(string buyerId, Dictionary<string, int> items, CancellationToken cancellationToken = default)
    {
        var db = valkey.GetDatabase();
        var key = BasketKey(buyerId);

        // Clear and replace (PUT semantics)
        await db.KeyDeleteAsync(key);

        if (items.Count > 0)
        {
            var hashEntries = items.Select(kvp => new HashEntry(kvp.Key, kvp.Value)).ToArray();
            await db.HashSetAsync(key, hashEntries);
        }

        // Refresh sliding 7-day TTL on every write
        await db.KeyExpireAsync(key, SlidingTtl);
        return true;
    }

    public async Task<bool> DeleteBasketAsync(string buyerId, CancellationToken cancellationToken = default)
    {
        var db = valkey.GetDatabase();
        return await db.KeyDeleteAsync(BasketKey(buyerId));
    }
}
