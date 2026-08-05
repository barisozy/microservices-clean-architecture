using Inventory.Application.Common.Interfaces;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Common.Interfaces;

// Compatibility adapter for legacy unit tests. Production code uses IInventoryWriteRepository.
public interface IInventoryDbContext : IInventoryWriteRepository
{
    DbSet<InventoryReservation> Reservations { get; }
    DbSet<Stock> Stocks { get; }
    new Task<InventoryReservation?> FindReservationAsync(Guid id, CancellationToken cancellationToken = default) => Reservations.Include(r => r.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    new Task<InventoryReservation?> FindReservationByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) => Reservations.Include(r => r.Items).FirstOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
    new Task<Stock?> FindStockAsync(string sku, CancellationToken cancellationToken = default) => Stocks.FirstOrDefaultAsync(x => x.Sku == sku, cancellationToken);
    new void Add(InventoryReservation reservation) => Reservations.Add(reservation);
    new void Add(Stock stock) => Stocks.Add(stock);
    Task<InventoryReservation?> IInventoryWriteRepository.FindReservationAsync(Guid id, CancellationToken cancellationToken) => Reservations.Include(r => r.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    Task<InventoryReservation?> IInventoryWriteRepository.FindReservationByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) => Reservations.Include(r => r.Items).FirstOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
    Task<Stock?> IInventoryWriteRepository.FindStockAsync(string sku, CancellationToken cancellationToken) => Stocks.FirstOrDefaultAsync(x => x.Sku == sku, cancellationToken);
    void IInventoryWriteRepository.Add(InventoryReservation reservation) => Reservations.Add(reservation);
    void IInventoryWriteRepository.Add(Stock stock) => Stocks.Add(stock);
}
