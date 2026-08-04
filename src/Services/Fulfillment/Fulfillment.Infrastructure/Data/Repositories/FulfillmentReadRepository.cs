using Fulfillment.Application.Common.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace Fulfillment.Infrastructure.Data.Repositories;

public class FulfillmentReadRepository(IConnectionMultiplexer valkey) : IFulfillmentReadRepository
{
    private readonly IDatabase _database = valkey.GetDatabase();
    private const string Prefix = "fulfillment-read-model:";
    private const string StatusPrefix = "fulfillment-status:";

    public async Task<ShipmentReadModel?> GetShipmentAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var value = await _database.StringGetAsync(StatusPrefix + orderId);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<ShipmentReadModel>(value.ToString());
    }

    public async Task SetShipmentAsync(ShipmentReadModel shipment, CancellationToken cancellationToken)
    {
        await _database.StringSetAsync(Prefix + shipment.OrderId, JsonSerializer.Serialize(shipment));
    }

    public async Task<string?> GetFulfillmentStatusAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var value = await _database.StringGetAsync(Prefix + orderId);
        if (value.IsNullOrEmpty) return null;

        return value.ToString();
    }

    public async Task SetFulfillmentStatusAsync(Guid orderId, string status, CancellationToken cancellationToken)
    {
        await _database.StringSetAsync(StatusPrefix + orderId, status);
    }
}
