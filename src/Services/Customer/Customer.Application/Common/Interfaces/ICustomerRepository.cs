using Customer.Domain.Entities;

namespace Customer.Application;

public interface ICustomerRepository
{
    Task<CustomerProfile?> GetProfileAsync(Guid subject, CancellationToken cancellationToken);
    Task<CustomerProfile> UpsertProfileAsync(Guid subject, string displayName, string email, CancellationToken cancellationToken);
    Task<IReadOnlyList<Address>> GetAddressesAsync(Guid subject, CancellationToken cancellationToken);
    Task<Address> AddAddressAsync(Guid subject, string line1, string city, string postalCode, CancellationToken cancellationToken);
}
