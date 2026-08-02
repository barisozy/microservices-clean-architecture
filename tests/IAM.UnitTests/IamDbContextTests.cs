using IAM.Domain.Entities;
using IAM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace IAM.UnitTests;

public class IamDbContextTests
{
    [Fact]
    public void Model_ShouldConfigureProfileInvitationAndMembershipKeys()
    {
        using var db = new IamDbContext(new DbContextOptionsBuilder<IamDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Model.FindEntityType(typeof(IamProfile))!.FindPrimaryKey()!.Properties.Single().Name.ShouldBe(nameof(IamProfile.KeycloakSubject));
        db.Model.FindEntityType(typeof(Invitation))!.GetIndexes().ShouldContain(index => index.Properties.Single().Name == nameof(Invitation.IdempotencyKey) && index.IsUnique);
        db.Model.FindEntityType(typeof(GroupMembership))!.FindPrimaryKey()!.Properties.Count.ShouldBe(2);
    }
}
