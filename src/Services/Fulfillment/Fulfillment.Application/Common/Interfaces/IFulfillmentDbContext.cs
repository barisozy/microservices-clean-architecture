using Fulfillment.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fulfillment.Application.Common.Interfaces;

public interface IUser
{
    string? Id { get; }
}

public interface IFulfillmentDbContext
{
    DbSet<FulfillmentTask> Tasks { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
