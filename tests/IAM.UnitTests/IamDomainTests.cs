using System;
using IAM.Domain.Entities;
using Moq;
using Shouldly;
using Xunit;

namespace IAM.UnitTests;

public class IamDomainTests
{
    [Fact]
    public void IamProfile_Initialization_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var subject = Guid.NewGuid();
        var name = "John Doe";
        var email = "john@example.com";

        // Act
        var profile = new IamProfile
        {
            KeycloakSubject = subject,
            DisplayName = name,
            Email = email
        };

        // Assert
        profile.KeycloakSubject.ShouldBe(subject);
        profile.DisplayName.ShouldBe(name);
        profile.Email.ShouldBe(email);
        profile.CreatedAt.ShouldNotBe(default);
    }

    [Fact]
    public void Invitation_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var invitation = new Invitation
        {
            Email = "admin@example.com",
            Role = "ADMIN",
            IdempotencyKey = Guid.NewGuid()
        };

        // Assert
        invitation.Id.ShouldNotBe(Guid.Empty);
        invitation.Role.ShouldBe("ADMIN");
        invitation.Status.ShouldBe("PENDING");
        invitation.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public void MockService_UsingMoq_ShouldBeSupported()
    {
        // Arrange
        var mockService = new Mock<IDisposable>();
        mockService.Setup(s => s.Dispose());

        // Act
        mockService.Object.Dispose();

        // Assert
        mockService.Verify(s => s.Dispose(), Times.Once);
    }
}
