using Inventory.Domain.Common;
using Inventory.Domain.Entities;

namespace Inventory.Domain.Events;

public sealed class StockReservedDomainEvent(InventoryReservation reservation) : BaseEvent
{
    public InventoryReservation Reservation { get; } = reservation;
}
