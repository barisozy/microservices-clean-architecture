namespace Fulfillment.Application.Common.Interfaces;

public interface IFulfillmentReadRepository
{
    Task<ShipmentReadModel?> GetShipmentAsync(Guid orderId, CancellationToken cancellationToken);
    Task SetShipmentAsync(ShipmentReadModel shipment, CancellationToken cancellationToken);
    Task<string?> GetFulfillmentStatusAsync(Guid orderId, CancellationToken cancellationToken);
    Task SetFulfillmentStatusAsync(Guid orderId, string status, CancellationToken cancellationToken);
}

public sealed record ShipmentReadModel(Guid OrderId, string TrackingNumber, string Status, DateTime ShippedAt);
