namespace Order.Domain.Enums;

public enum OrderStatus
{
    Pending = 1,
    Paid = 2,
    Processing = 3,
    Shipped = 4,
    Delivered = 5,
    Cancelled = 6,
    PendingInventory = 7,
    AwaitingPayment = 8,
    Failed = 9
}
