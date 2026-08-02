using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

public class Stock : BaseAuditableEntity
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }

    public int AvailableQuantity => Quantity - ReservedQuantity;

    public Stock() { }

    public Stock(string sku, int initialQuantity)
    {
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("SKU is required.", nameof(sku));
        if (initialQuantity < 0) throw new ArgumentOutOfRangeException(nameof(initialQuantity));
        Sku = sku;
        Quantity = initialQuantity;
        ReservedQuantity = 0;
    }

    public bool Reserve(int quantity)
    {
        if (quantity <= 0) return false;
        if (AvailableQuantity < quantity) return false;
        ReservedQuantity += quantity;
        return true;
    }

    public void Release(int quantity)
    {
        if (quantity <= 0) return;
        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
    }

    public void SetQuantity(int quantity)
    {
        if (quantity < ReservedQuantity)
            throw new InvalidOperationException("Total quantity cannot be lower than the reserved quantity.");
        Quantity = quantity;
    }
}

public class InventoryReservation : BaseAuditableEntity
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
        return new InventoryReservation
        {
            OrderId = orderId,
            Sku = sku,
            Quantity = quantity,
            IsReleased = false
        };
    }

    public void Release()
    {
        IsReleased = true;
    }
}
