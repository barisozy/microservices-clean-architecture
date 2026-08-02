using ECommerce.Contracts.Protos;
using IAM.Api.Services;
using IAM.Application;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace IAM.UnitTests;

public class IamGrpcServiceTests
{
    [Fact]
    public async Task CheckPermission_ShouldRejectMalformedSubjectAndReturnApplicationDecision()
    {
        var subject = Guid.CreateVersion7();
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.Is<CheckPermissionQuery>(query => query.Subject == subject.ToString()),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionResult(true, "CUSTOMER"));
        var service = new IamGrpcService(
            sender.Object,
            new CheckPermissionQueryValidator(),
            NullLogger<IamGrpcService>.Instance);

        var malformed = await service.CheckPermission(
            new CheckPermissionRequest { Subject = "", Permission = "Catalog.Write" }, null!);
        var known = await service.CheckPermission(
            new CheckPermissionRequest { Subject = subject.ToString(), Permission = "Catalog.Read" }, null!);

        malformed.Allowed.ShouldBeFalse();
        malformed.Role.ShouldBe("GUEST");
        known.Allowed.ShouldBeTrue();
        known.Role.ShouldBe("CUSTOMER");
    }
}
