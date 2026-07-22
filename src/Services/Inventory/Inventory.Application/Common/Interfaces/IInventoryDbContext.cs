using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Common.Interfaces;

public interface IUser
{
    string? Id { get; }
}

public interface IInventoryDbContext
{
    DbSet<InventoryReservation> Reservations { get; }
    DbSet<Stock> Stocks { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
