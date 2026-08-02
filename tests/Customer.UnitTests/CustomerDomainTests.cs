using System;
using Customer.Domain.Entities;
using Moq;
using Shouldly;
using Xunit;

namespace Customer.UnitTests;

public class CustomerDomainTests
{
    [Fact]
    public void CustomerProfile_Initialization_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var sub = Guid.NewGuid();
        var name = "Jane Smith";
        var email = "jane@example.com";

        // Act
        var profile = new CustomerProfile
        {
            KeycloakSubject = sub,
            DisplayName = name,
            Email = email
        };

        // Assert
        profile.KeycloakSubject.ShouldBe(sub);
        profile.DisplayName.ShouldBe(name);
        profile.Email.ShouldBe(email);
    }

    [Fact]
    public void Address_Initialization_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var customerId = Guid.NewGuid();

        // Act
        var address = new Address
        {
            CustomerId = customerId,
            Line1 = "123 Main St",
            City = "Tech City",
            PostalCode = "12345"
        };

        // Assert
        address.Id.ShouldNotBe(Guid.Empty);
        address.CustomerId.ShouldBe(customerId);
        address.Line1.ShouldBe("123 Main St");
        address.City.ShouldBe("Tech City");
        address.PostalCode.ShouldBe("12345");
    }

    [Fact]
    public void CustomerPreference_Initialization_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var customerId = Guid.NewGuid();

        // Act
        var preference = new CustomerPreference
        {
            CustomerId = customerId,
            Key = "Theme",
            Value = "Dark"
        };

        // Assert
        preference.Id.ShouldNotBe(Guid.Empty);
        preference.CustomerId.ShouldBe(customerId);
        preference.Key.ShouldBe("Theme");
        preference.Value.ShouldBe("Dark");
    }

    [Fact]
    public void MockCustomerService_UsingMoq_ShouldBeSupported()
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
