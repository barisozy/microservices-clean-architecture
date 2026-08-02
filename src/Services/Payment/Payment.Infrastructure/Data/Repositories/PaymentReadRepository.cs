using Payment.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Payment.Infrastructure.Data.Repositories;

public class PaymentReadRepository(IConnectionMultiplexer valkey) : IPaymentReadRepository
{
    private readonly IDatabase _database = valkey.GetDatabase();
    private const string Prefix = "payment-read-model:";

    public async Task<string?> GetPaymenttatusAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var value = await _database.StringGetAsync(Prefix + orderId);
        if (value.IsNullOrEmpty) return null;

        return value.ToString();
    }

    public async Task SetPaymenttatusAsync(Guid orderId, string status, CancellationToken cancellationToken)
    {
        await _database.StringSetAsync(Prefix + orderId, status);
    }
}

