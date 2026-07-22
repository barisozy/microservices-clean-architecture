namespace Inventory.Application.Common.Interfaces;

public interface IStockReadRepository
{
    Task<int?> GetAvailableQuantityAsync(string sku, CancellationToken cancellationToken);
    Task SetAvailableQuantityAsync(string sku, int quantity, CancellationToken cancellationToken);
}
