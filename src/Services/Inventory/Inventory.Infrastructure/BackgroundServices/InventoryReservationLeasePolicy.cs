using Inventory.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure.BackgroundServices;

public sealed class InventoryReservationLeasePolicy(
    TimeProvider timeProvider,
    IOptions<InventoryReservationOptions> options) : IInventoryReservationLeasePolicy
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateTimeOffset GetExpiry(DateTimeOffset now) => now.Add(options.Value.LeaseDuration);
}
