namespace Order.Application.Common.Interfaces;

/// <summary>
/// Order-owned disposable state stored in Valkey. Keeping this port in the
/// Application layer prevents RESP client details from crossing the onion boundary.
/// PostgreSQL remains the authority for order idempotency.
/// </summary>
public interface IOrderCache
{
    Task<Guid?> GetOrderIdAsync(Guid customerId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task SetOrderIdAsync(Guid customerId, string idempotencyKey, Guid orderId, CancellationToken cancellationToken = default);
    Task<decimal?> GetCatalogPriceAsync(string sku, CancellationToken cancellationToken = default);
    Task SetCatalogPriceAsync(string sku, decimal price, CancellationToken cancellationToken = default);
    Task<IAsyncDisposable?> TryAcquireBasketLockAsync(
        string keycloakSubject,
        CancellationToken cancellationToken = default);
}
