using System;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.Auditing;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Http;
using Moq;
using Shouldly;
using Xunit;

namespace Auditing.UnitTests;

public class TestCommand : IRequest<string> { }
public class TestQuery : IRequest<string> { }

public class AuditBehaviorTests
{
    [Fact]
    public async Task Handle_ShouldPublishAuditLog_ForCommand_WithFullHttpContext()
    {
        // Arrange
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        var context = new DefaultHttpContext();
        var claims = new[] 
        { 
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"), 
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Manager")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        context.User = new ClaimsPrincipal(identity);
        context.Request.Headers.UserAgent = "TestAgent/1.0";
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        context.TraceIdentifier = "trace-id-123";

        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(context);

        var behavior = new AuditBehavior<TestCommand, string>(publishEndpointMock.Object, httpContextAccessorMock.Object);

        // Act
        var result = await behavior.Handle(new TestCommand(), () => Task.FromResult("Result"), CancellationToken.None);

        // Assert
        result.ShouldBe("Result");
        publishEndpointMock.Verify(x => x.Publish(It.Is<AuditLogCreated>(msg => 
            msg.UserId == "test-user-id" &&
            msg.UserRoles == "Admin,Manager" &&
            msg.EntityName == "TestCommand" &&
            msg.Action == "Execute" &&
            msg.IpAddress == "127.0.0.1" &&
            msg.UserAgent == "TestAgent/1.0" &&
            msg.TraceId == "trace-id-123"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPublishAuditLog_WhenHttpContextIsNull()
    {
        // Arrange
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        var behavior = new AuditBehavior<TestCommand, string>(publishEndpointMock.Object, httpContextAccessorMock.Object);

        // Act
        var result = await behavior.Handle(new TestCommand(), () => Task.FromResult("OK"), CancellationToken.None);

        // Assert
        result.ShouldBe("OK");
        publishEndpointMock.Verify(x => x.Publish(It.Is<AuditLogCreated>(msg => 
            msg.UserId == "System" &&
            msg.UserRoles == "" &&
            msg.IpAddress == "Unknown" &&
            msg.UserAgent == "Unknown" &&
            msg.TraceId == "Unknown"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotPublishAuditLog_ForQuery()
    {
        // Arrange
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        var behavior = new AuditBehavior<TestQuery, string>(publishEndpointMock.Object, httpContextAccessorMock.Object);

        // Act
        var result = await behavior.Handle(new TestQuery(), () => Task.FromResult("Result"), CancellationToken.None);

        // Assert
        result.ShouldBe("Result");
        publishEndpointMock.Verify(x => x.Publish(It.IsAny<AuditLogCreated>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldUseActivityCurrentId_WhenActivityIsRunning()
    {
        // Arrange
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(new DefaultHttpContext());

        using var activity = new Activity("TestActivity").Start();

        var behavior = new AuditBehavior<TestCommand, string>(publishEndpointMock.Object, httpContextAccessorMock.Object);

        // Act
        await behavior.Handle(new TestCommand(), () => Task.FromResult("Done"), CancellationToken.None);

        // Assert
        publishEndpointMock.Verify(x => x.Publish(It.Is<AuditLogCreated>(msg => 
            msg.TraceId == activity.Id
        ), It.IsAny<CancellationToken>()), Times.Once);
    }
}
