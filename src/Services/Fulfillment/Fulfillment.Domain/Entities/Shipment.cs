namespace Fulfillment.Domain.Entities;

public class Shipment
{
    public Guid OrderId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "SHIPPED";
    public DateTime ShippedAt { get; set; } = DateTime.UtcNow;
}
