using ECommerce.Contracts.Protos;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Promotion.Api.Services;
using Promotion.Application;
using Shouldly;
using Xunit;

namespace Promotion.UnitTests;

public class PromotionGrpcServiceTests
{
    [Fact]
    public async Task ApplyCoupon_ShouldReturnApplicationCalculationResults()
    {
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(It.Is<ApplyCouponQuery>(query => query.Code == "PCT10"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplyCouponResult(90m, true, "Coupon applied successfully."));
        sender.Setup(value => value.Send(It.Is<ApplyCouponQuery>(query => query.Code == "FIX20"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApplyCouponResult(0m, true, "Coupon applied successfully."));
        sender.Setup(value => value.Send(
                It.Is<ApplyCouponQuery>(query => query.Code == "OLD" || query.Code == "NONE"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplyCouponQuery query, CancellationToken _) => new ApplyCouponResult(query.OrderTotal, false, "Coupon code invalid or expired."));
        var service = new PromotionGrpcService(
            sender.Object,
            new ApplyCouponQueryValidator(),
            NullLogger<PromotionGrpcService>.Instance);

        (await service.ApplyCoupon(new ApplyCouponRequest { Code = "PCT10", OrderTotal = new Money { MinorUnits = 10000, Currency = "USD" } }, null!)).DiscountedTotal.MinorUnits.ShouldBe(9000L);
        (await service.ApplyCoupon(new ApplyCouponRequest { Code = "FIX20", OrderTotal = new Money { MinorUnits = 1500, Currency = "USD" } }, null!)).DiscountedTotal.MinorUnits.ShouldBe(0L);
        (await service.ApplyCoupon(new ApplyCouponRequest { Code = "OLD", OrderTotal = new Money { MinorUnits = 10000, Currency = "USD" } }, null!)).IsValid.ShouldBeFalse();
        (await service.ApplyCoupon(new ApplyCouponRequest { Code = "NONE", OrderTotal = new Money { MinorUnits = 10000, Currency = "USD" } }, null!)).IsValid.ShouldBeFalse();
    }
}
