using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

public sealed class Stock : BaseAuditableEntity
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
        Sku = sku; Quantity = initialQuantity;
    }
    public bool Reserve(int quantity)
    {
        if (quantity <= 0 || AvailableQuantity < quantity) return false;
        ReservedQuantity += quantity; return true;
    }
    public void Release(int quantity) { if (quantity > 0) ReservedQuantity = Math.Max(0, ReservedQuantity - quantity); }
    public void SetQuantity(int quantity)
    {
        if (quantity < ReservedQuantity) throw new InvalidOperationException("Total quantity cannot be lower than the reserved quantity.");
        Quantity = quantity;
    }
}
