using Fulfillment.Application.Common.Interfaces;
using Fulfillment.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fulfillment.Application.Common.Interfaces;

// Compatibility adapter for legacy unit tests. Production code uses IFulfillmentWriteRepository.
public interface IFulfillmentDbContext : IFulfillmentWriteRepository
{
    DbSet<FulfillmentTask> Tasks { get; }
    DbSet<Shipment> Shipments { get; }
    new Task<Shipment?> FindShipmentAsync(Guid orderId, CancellationToken cancellationToken = default) => Shipments.FirstOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
    new void Add(FulfillmentTask task) => Tasks.Add(task);
    new void Add(Shipment shipment) => Shipments.Add(shipment);
}
