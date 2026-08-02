using Customer.Application;
using Customer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Customer.Infrastructure.Data;

public sealed class CustomerRepository(CustomerDbContext dbContext) : ICustomerRepository
{
    public Task<CustomerProfile?> GetProfileAsync(Guid subject, CancellationToken cancellationToken) =>
        dbContext.Profiles.AsNoTracking().FirstOrDefaultAsync(profile => profile.KeycloakSubject == subject, cancellationToken);

    public async Task<CustomerProfile> UpsertProfileAsync(
        Guid subject,
        string displayName,
        string email,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.Profiles.FirstOrDefaultAsync(item => item.KeycloakSubject == subject, cancellationToken);
        if (profile is null)
        {
            profile = new CustomerProfile { KeycloakSubject = subject, DisplayName = displayName, Email = email };
            dbContext.Profiles.Add(profile);
        }
        else
        {
            profile.DisplayName = displayName;
            profile.Email = email;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task<IReadOnlyList<Address>> GetAddressesAsync(Guid subject, CancellationToken cancellationToken) =>
        await dbContext.Addresses.AsNoTracking().Where(address => address.CustomerId == subject).ToListAsync(cancellationToken);

    public async Task<Address> AddAddressAsync(
        Guid subject,
        string line1,
        string city,
        string postalCode,
        CancellationToken cancellationToken)
    {
        var address = new Address
        {
            CustomerId = subject,
            Line1 = line1,
            City = city,
            PostalCode = postalCode
        };
        dbContext.Addresses.Add(address);
        await dbContext.SaveChangesAsync(cancellationToken);
        return address;
    }
}
