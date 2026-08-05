using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Promotion.Application;
using Promotion.Application.Common.Interfaces;
using Promotion.Domain.Entities;
using Shouldly;
using Xunit;

namespace Promotion.UnitTests;

public class ApplyCouponQueryHandlerTests
{
    private readonly Mock<IPromotionRepository> _repositoryMock = new();

    [Fact]
    public async Task Handle_CouponNull_ReturnsInvalid()
    {
        _repositoryMock.Setup(x => x.GetCouponAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Coupon?)null);

        var handler = new ApplyCouponQueryHandler(_repositoryMock.Object);
        var result = await handler.Handle(new ApplyCouponQuery("CODE", 100m), CancellationToken.None);

        result.IsValid.ShouldBeFalse();
        result.DiscountedTotal.ShouldBe(100m);
    }

    [Fact]
    public async Task Handle_CouponExpired_ReturnsInvalid()
    {
        var coupon = new Coupon { Code = "CODE", ExpiresAt = DateTime.UtcNow.AddDays(-1) };
        _repositoryMock.Setup(x => x.GetCouponAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new ApplyCouponQueryHandler(_repositoryMock.Object);
        var result = await handler.Handle(new ApplyCouponQuery("CODE", 100m), CancellationToken.None);

        result.IsValid.ShouldBeFalse();
        result.DiscountedTotal.ShouldBe(100m);
    }

    [Fact]
    public async Task Handle_PercentageDiscount_AppliesDiscount()
    {
        var coupon = new Coupon { Code = "CODE", DiscountType = "PERCENTAGE", Value = 20, ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _repositoryMock.Setup(x => x.GetCouponAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new ApplyCouponQueryHandler(_repositoryMock.Object);
        var result = await handler.Handle(new ApplyCouponQuery("CODE", 100m), CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.DiscountedTotal.ShouldBe(80m); // 20% of 100
    }

    [Fact]
    public async Task Handle_FixedDiscount_AppliesDiscount()
    {
        var coupon = new Coupon { Code = "CODE", DiscountType = "FIXED", Value = 30, ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _repositoryMock.Setup(x => x.GetCouponAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new ApplyCouponQueryHandler(_repositoryMock.Object);
        var result = await handler.Handle(new ApplyCouponQuery("CODE", 100m), CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.DiscountedTotal.ShouldBe(70m); // 100 - 30
    }

    [Fact]
    public async Task Handle_FixedDiscount_DoesNotDropBelowZero()
    {
        var coupon = new Coupon { Code = "CODE", DiscountType = "FIXED", Value = 150, ExpiresAt = DateTime.UtcNow.AddDays(1) };
        _repositoryMock.Setup(x => x.GetCouponAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new ApplyCouponQueryHandler(_repositoryMock.Object);
        var result = await handler.Handle(new ApplyCouponQuery("CODE", 100m), CancellationToken.None);

        result.IsValid.ShouldBeTrue();
        result.DiscountedTotal.ShouldBe(0m);
    }
}
