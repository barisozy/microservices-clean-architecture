using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

public sealed class InventoryReservation : BaseAuditableEntity
{
    public Guid OrderId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool IsReleased { get; set; }
    public static InventoryReservation Create(Guid orderId, string sku, int quantity)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId is required.", nameof(orderId));
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU is required.", nameof(sku));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        return new InventoryReservation { OrderId = orderId, Sku = sku, Quantity = quantity };
    }
    public void Release() => IsReleased = true;
}
