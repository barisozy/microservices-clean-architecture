using ECommerce.Contracts.Protos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Promotion.Api.Services;
using Promotion.Domain.Entities;
using Promotion.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace Promotion.UnitTests;

public class PromotionGrpcServiceTests
{
    [Fact]
    public async Task ApplyCoupon_ShouldHandlePercentageFixedExpiredAndMissingCoupons()
    {
        await using var db = new PromotionDbContext(new DbContextOptionsBuilder<PromotionDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Coupons.AddRange(
            new Coupon { Code = "PCT10", DiscountType = "PERCENTAGE", Value = 10, ExpiresAt = DateTime.UtcNow.AddDays(1) },
            new Coupon { Code = "FIX20", DiscountType = "FIXED", Value = 20, ExpiresAt = DateTime.UtcNow.AddDays(1) },
            new Coupon { Code = "OLD", DiscountType = "FIXED", Value = 20, ExpiresAt = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new PromotionGrpcService(db, NullLogger<PromotionGrpcService>.Instance);

        (await service.ApplyCoupon(new ApplyCouponRequest { Code = "PCT10", OrderTotal = 100 }, null!)).DiscountedTotal.ShouldBe(90d);
        (await service.ApplyCoupon(new ApplyCouponRequest { Code = "FIX20", OrderTotal = 15 }, null!)).DiscountedTotal.ShouldBe(0d);
        (await service.ApplyCoupon(new ApplyCouponRequest { Code = "OLD", OrderTotal = 100 }, null!)).IsValid.ShouldBeFalse();
        (await service.ApplyCoupon(new ApplyCouponRequest { Code = "NONE", OrderTotal = 100 }, null!)).IsValid.ShouldBeFalse();
    }
}
