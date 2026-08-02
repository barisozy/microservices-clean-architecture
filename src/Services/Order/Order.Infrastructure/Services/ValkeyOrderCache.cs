using System.Globalization;
using Medallion.Threading.Redis;
using Order.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Order.Infrastructure.Services;

public sealed class ValkeyOrderCache(IConnectionMultiplexer valkey) : IOrderCache
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan PriceTtl = TimeSpan.FromDays(1);
    private readonly IDatabase _database = valkey.GetDatabase();
    private readonly RedisDistributedSynchronizationProvider _locks =
        new(valkey.GetDatabase());

    public async Task<Guid?> GetOrderIdAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.StringGetAsync(IdempotencyKey(idempotencyKey));
        return value.HasValue && Guid.TryParse(value.ToString(), out var orderId)
            ? orderId
            : null;
    }

    public async Task SetOrderIdAsync(
        string idempotencyKey,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.StringSetAsync(
            IdempotencyKey(idempotencyKey),
            orderId.ToString("D"),
            IdempotencyTtl);
    }

    public async Task<decimal?> GetCatalogPriceAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _database.StringGetAsync(CatalogPriceKey(sku));
        return value.HasValue
            && decimal.TryParse(
                value.ToString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var price)
            ? price
            : null;
    }

    public async Task SetCatalogPriceAsync(
        string sku,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.StringSetAsync(
            CatalogPriceKey(sku),
            price.ToString(CultureInfo.InvariantCulture),
            PriceTtl);
    }

    public async Task<IAsyncDisposable?> TryAcquireBasketLockAsync(
        string keycloakSubject,
        CancellationToken cancellationToken = default) =>
        await _locks
            .CreateLock($"lock:basket:{keycloakSubject}")
            .TryAcquireAsync(TimeSpan.Zero, cancellationToken);

    private static string IdempotencyKey(string key) => $"idempotency:order:{key}";
    private static string CatalogPriceKey(string sku) => $"catalog:price:{sku}";
}
