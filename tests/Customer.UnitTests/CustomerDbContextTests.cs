using Customer.Domain.Entities;
using Customer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Customer.UnitTests;

public class CustomerDbContextTests
{
    [Fact]
    public void Model_ShouldConfigureProfileAndOwnedEntityKeys()
    {
        using var db = new CustomerDbContext(new DbContextOptionsBuilder<CustomerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Model.FindEntityType(typeof(CustomerProfile))!.FindPrimaryKey()!.Properties.Single().Name.ShouldBe(nameof(CustomerProfile.KeycloakSubject));
        db.Model.FindEntityType(typeof(Address))!.FindPrimaryKey()!.Properties.Single().Name.ShouldBe(nameof(Address.Id));
        db.Model.FindEntityType(typeof(CustomerPreference))!.FindPrimaryKey()!.Properties.Single().Name.ShouldBe(nameof(CustomerPreference.Id));
    }
}
