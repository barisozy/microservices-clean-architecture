using System.Text.Json;
using Ordering.Application.Common.Interfaces;
using Ordering.Application.Orders.Queries;
using StackExchange.Redis;

namespace Ordering.Infrastructure.Data.Repositories;

public class OrderReadRepository(IConnectionMultiplexer valkey) : IOrderReadRepository
{
    private readonly IDatabase _database = valkey.GetDatabase();
    private const string Prefix = "order-read-model:";

    public async Task<OrderStatusDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var value = await _database.StringGetAsync(Prefix + orderId);
        if (value.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<OrderStatusDto>(value.ToString()!);
    }

    public async Task SetOrderAsync(OrderStatusDto order, CancellationToken cancellationToken)
    {
        var value = JsonSerializer.Serialize(order);
        // Store read model in Valkey (can set expiration or keep indefinitely depending on requirements)
        await _database.StringSetAsync(Prefix + order.Id, value);
    }
}
