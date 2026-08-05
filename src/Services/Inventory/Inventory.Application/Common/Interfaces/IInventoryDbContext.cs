using Inventory.Domain.Entities;

namespace Inventory.Application.Common.Interfaces;

public interface IInventoryWriteRepository
{
    Task<InventoryReservation?> FindReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<InventoryReservation?> FindReservationByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<Stock?> FindStockAsync(string sku, CancellationToken cancellationToken = default);
    void Add(InventoryReservation reservation);
    void Add(Stock stock);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
