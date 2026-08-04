using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

public sealed class InventoryReservationItem : BaseEntity
{
    public Guid InventoryReservationId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public int Quantity { get; private set; }

    private InventoryReservationItem() { }

    public static InventoryReservationItem Create(Guid reservationId, string sku, int quantity)
    {
        return new InventoryReservationItem
        {
            Id = Guid.CreateVersion7(),
            InventoryReservationId = reservationId,
            Sku = sku,
            Quantity = quantity
        };
    }
}