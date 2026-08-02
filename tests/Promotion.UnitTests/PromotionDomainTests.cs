using System;
using Promotion.Domain.Entities;
using Moq;
using Shouldly;
using Xunit;

namespace Promotion.UnitTests;

public class PromotionDomainTests
{
    [Fact]
    public void Coupon_Initialization_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var code = "SUMMER20";
        var discountType = "PERCENTAGE";
        var value = 20.0m;

        // Act
        var coupon = new Coupon
        {
            Code = code,
            DiscountType = discountType,
            Value = value
        };

        // Assert
        coupon.Id.ShouldNotBe(Guid.Empty);
        coupon.Code.ShouldBe(code);
        coupon.DiscountType.ShouldBe(discountType);
        coupon.Value.ShouldBe(value);
        coupon.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public void Campaign_Initialization_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var name = "Black Friday Sale";

        // Act
        var campaign = new Campaign
        {
            Name = name
        };

        // Assert
        campaign.Id.ShouldNotBe(Guid.Empty);
        campaign.Name.ShouldBe(name);
        campaign.StartDate.ShouldNotBe(default);
        campaign.EndDate.ShouldBeGreaterThan(campaign.StartDate);
    }

    [Fact]
    public void MockPromotionService_UsingMoq_ShouldBeSupported()
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
