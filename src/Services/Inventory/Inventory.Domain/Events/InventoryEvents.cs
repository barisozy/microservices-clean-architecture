using Inventory.Domain.Common;
using Inventory.Domain.Entities;

namespace Inventory.Domain.Events;

public class StockReservedDomainEvent : BaseEvent
{
    public InventoryReservation Reservation { get; }

    public StockReservedDomainEvent(InventoryReservation reservation)
    {
        Reservation = reservation;
    }
}

public class StockReleasedDomainEvent : BaseEvent
{
    public InventoryReservation Reservation { get; }

    public StockReleasedDomainEvent(InventoryReservation reservation)
    {
        Reservation = reservation;
    }
}
