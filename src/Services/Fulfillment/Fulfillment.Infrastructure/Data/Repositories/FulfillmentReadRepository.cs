using Fulfillment.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Fulfillment.Infrastructure.Data.Repositories;

public class FulfillmentReadRepository(IConnectionMultiplexer valkey) : IFulfillmentReadRepository
{
    private readonly IDatabase _database = valkey.GetDatabase();
    private const string Prefix = "fulfillment-read-model:";

    public async Task<string?> GetFulfillmentStatusAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var value = await _database.StringGetAsync(Prefix + orderId);
        if (value.IsNullOrEmpty) return null;

        return value.ToString();
    }

    public async Task SetFulfillmentStatusAsync(Guid orderId, string status, CancellationToken cancellationToken)
    {
        await _database.StringSetAsync(Prefix + orderId, status);
    }
}
