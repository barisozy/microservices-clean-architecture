using Fulfillment.Domain.Common;

namespace Fulfillment.Domain.Entities;

public class FulfillmentTask : BaseAuditableEntity
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = "Pending";
    public string TrackingNumber { get; set; } = string.Empty;
}
