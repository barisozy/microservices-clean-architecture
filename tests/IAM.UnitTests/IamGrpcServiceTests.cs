using ECommerce.Contracts.Protos;
using IAM.Api.Services;
using IAM.Domain.Entities;
using IAM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace IAM.UnitTests;

public class IamGrpcServiceTests
{
    [Fact]
    public async Task CheckPermission_ShouldRejectMalformedSubjectAndClassifyKnownAndUnknownSubjects()
    {
        await using var db = new IamDbContext(new DbContextOptionsBuilder<IamDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var subject = Guid.CreateVersion7();
        db.Profiles.Add(new IamProfile { KeycloakSubject = subject, Email = "member@example.test" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new IamGrpcService(db, NullLogger<IamGrpcService>.Instance);

        var malformed = await service.CheckPermission(new CheckPermissionRequest { Subject = "not-a-guid", Permission = "Catalog.Write" }, null!);
        var known = await service.CheckPermission(new CheckPermissionRequest { Subject = subject.ToString(), Permission = "Catalog.Read" }, null!);
        var unknown = await service.CheckPermission(new CheckPermissionRequest { Subject = Guid.CreateVersion7().ToString(), Permission = "Catalog.Read" }, null!);

        malformed.Allowed.ShouldBeFalse();
        malformed.Role.ShouldBe("GUEST");
        known.Role.ShouldBe("CUSTOMER");
        unknown.Role.ShouldBe("ADMIN");
    }
}
