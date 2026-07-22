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
        Sku = sku;
        Quantity = initialQuantity;
        ReservedQuantity = 0;
    }

    public bool Reserve(int quantity)
    {
        if (AvailableQuantity < quantity) return false;
        ReservedQuantity += quantity;
        return true;
    }

    public void Release(int quantity)
    {
        ReservedQuantity = Math.Max(0, ReservedQuantity - quantity);
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
