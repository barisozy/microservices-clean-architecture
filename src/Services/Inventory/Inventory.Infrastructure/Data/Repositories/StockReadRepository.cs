using Inventory.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Inventory.Infrastructure.Data.Repositories;

public class StockReadRepository(IConnectionMultiplexer valkey) : IStockReadRepository
{
    private readonly IDatabase _database = valkey.GetDatabase();
    private const string Prefix = "stock-read-model:";

    public async Task<int?> GetAvailableQuantityAsync(string sku, CancellationToken cancellationToken)
    {
        var value = await _database.StringGetAsync(Prefix + sku);
        if (value.IsNullOrEmpty) return null;

        if (int.TryParse(value.ToString(), out var quantity))
        {
            return quantity;
        }
        return null;
    }

    public async Task SetAvailableQuantityAsync(string sku, int quantity, CancellationToken cancellationToken)
    {
        await _database.StringSetAsync(Prefix + sku, quantity.ToString());
    }
}
