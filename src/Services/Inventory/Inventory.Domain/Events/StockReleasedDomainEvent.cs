using Inventory.Domain.Common;
using Inventory.Domain.Entities;

namespace Inventory.Domain.Events;

public sealed class StockReleasedDomainEvent(InventoryReservation reservation) : BaseEvent
{
    public InventoryReservation Reservation { get; } = reservation;
}
