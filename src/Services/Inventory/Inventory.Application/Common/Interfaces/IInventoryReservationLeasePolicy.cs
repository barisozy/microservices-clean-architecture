namespace Inventory.Application.Common.Interfaces;

/// <summary>
/// Keeps the server-owned inventory lease policy outside the domain aggregate
/// while making the clock and duration controllable in tests.
/// </summary>
public interface IInventoryReservationLeasePolicy
{
    DateTimeOffset UtcNow { get; }

    DateTimeOffset GetExpiry(DateTimeOffset now);
}
