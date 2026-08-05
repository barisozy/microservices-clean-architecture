using Fulfillment.Domain.Entities;

namespace Fulfillment.Application.Common.Interfaces;

public interface IFulfillmentWriteRepository
{
    Task<Shipment?> FindShipmentAsync(Guid orderId, CancellationToken cancellationToken = default);
    void Add(FulfillmentTask task);
    void Add(Shipment shipment);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
