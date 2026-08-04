using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

public enum InventoryReservationStatus
{
    Pending = 0,
    Committed = 1,
    Released = 2,
    Expired = 3
}

public sealed class InventoryReservation : BaseAuditableEntity
{
    public Guid OrderId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public InventoryReservationStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CommittedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public DateTimeOffset? ExpiredAt { get; private set; }
    public uint Version { get; private set; }
    public bool IsReleased => Status is InventoryReservationStatus.Released or InventoryReservationStatus.Expired;
    public bool IsTerminal => Status is InventoryReservationStatus.Released or InventoryReservationStatus.Expired;

    private InventoryReservation() { }

    public static InventoryReservation Create(Guid orderId, string sku, int quantity, DateTimeOffset expiresAt)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId is required.", nameof(orderId));
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU is required.", nameof(sku));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (expiresAt == default) throw new ArgumentOutOfRangeException(nameof(expiresAt));
        return new InventoryReservation
        {
            Id = Guid.CreateVersion7(), OrderId = orderId, Sku = sku, Quantity = quantity,
            Status = InventoryReservationStatus.Pending, ExpiresAt = expiresAt
        };
    }

    public bool Commit(DateTimeOffset now)
    {
        if (Status == InventoryReservationStatus.Committed) return true;
        if (Status != InventoryReservationStatus.Pending || ExpiresAt <= now) return false;
        Status = InventoryReservationStatus.Committed;
        CommittedAt = now;
        return true;
    }

    public bool Release(DateTimeOffset now)
    {
        if (Status is InventoryReservationStatus.Released or InventoryReservationStatus.Expired) return false;
        Status = InventoryReservationStatus.Released;
        ReleasedAt = now;
        return true;
    }

    public bool Expire(DateTimeOffset now)
    {
        if (Status != InventoryReservationStatus.Pending || ExpiresAt > now) return false;
        Status = InventoryReservationStatus.Expired;
        ExpiredAt = now;
        return true;
    }
}
