using Inventory.Domain.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
    public string RequestFingerprint { get; private set; } = string.Empty;
    public InventoryReservationStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CommittedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public DateTimeOffset? ExpiredAt { get; private set; }
    public uint Version { get; private set; }
    public bool IsReleased => Status is InventoryReservationStatus.Released or InventoryReservationStatus.Expired;
    public bool IsTerminal => Status is InventoryReservationStatus.Released or InventoryReservationStatus.Expired;
    
    private readonly List<InventoryReservationItem> _items = new();
    public IReadOnlyCollection<InventoryReservationItem> Items => _items.AsReadOnly();
    
    // For backward compatibility in unit tests
    public string Sku => Items.FirstOrDefault()?.Sku ?? string.Empty;
    public int Quantity => Items.FirstOrDefault()?.Quantity ?? 0;

    private InventoryReservation() { }

    public static InventoryReservation Create(Guid orderId, Dictionary<string, int> items, DateTimeOffset expiresAt)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId is required.", nameof(orderId));
        if (items == null || items.Count == 0) throw new ArgumentException("Items are required.", nameof(items));
        if (expiresAt == default) throw new ArgumentOutOfRangeException(nameof(expiresAt));

        var reservation = new InventoryReservation
        {
            Id = Guid.CreateVersion7(),
            OrderId = orderId,
            Status = InventoryReservationStatus.Pending,
            ExpiresAt = expiresAt,
            RequestFingerprint = GenerateFingerprint(orderId, items)
        };

        foreach (var (sku, quantity) in items)
        {
            reservation._items.Add(InventoryReservationItem.Create(reservation.Id, sku, quantity));
        }

        return reservation;
    }

    public static InventoryReservation Create(Guid orderId, string sku, int quantity, DateTimeOffset expiresAt)
    {
        return Create(orderId, new Dictionary<string, int> { { sku, quantity } }, expiresAt);
    }

    public static string GenerateFingerprint(Guid orderId, Dictionary<string, int> items)
    {
        var sortedItems = items.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value}").ToList();
        var payload = $"{orderId:N}|{string.Join("|", sortedItems)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
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
