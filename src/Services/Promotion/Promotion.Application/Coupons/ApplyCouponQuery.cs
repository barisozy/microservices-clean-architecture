using FluentValidation;
using MediatR;
using Promotion.Application.Common.Interfaces;

namespace Promotion.Application;

public sealed record ApplyCouponQuery(string Code, decimal OrderTotal) : IRequest<ApplyCouponResult>;
public sealed record ApplyCouponResult(decimal DiscountedTotal, bool IsValid, string Message);
public sealed class ApplyCouponQueryValidator : AbstractValidator<ApplyCouponQuery>
{
    public ApplyCouponQueryValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(100);
        RuleFor(request => request.OrderTotal).GreaterThanOrEqualTo(0);
    }
}
public sealed class ApplyCouponQueryHandler(IPromotionRepository repository) : IRequestHandler<ApplyCouponQuery, ApplyCouponResult>
{
    public async Task<ApplyCouponResult> Handle(ApplyCouponQuery request, CancellationToken cancellationToken)
    {
        var coupon = await repository.GetCouponAsync(request.Code, cancellationToken);
        if (coupon is null || coupon.ExpiresAt < DateTime.UtcNow)
            return new ApplyCouponResult(request.OrderTotal, false, "Coupon code invalid or expired.");
        var discounted = coupon.DiscountType.Equals("PERCENTAGE", StringComparison.OrdinalIgnoreCase)
            ? request.OrderTotal * (1m - coupon.Value / 100m)
            : Math.Max(0m, request.OrderTotal - coupon.Value);
        return new ApplyCouponResult(discounted, true, "Coupon applied successfully.");
    }
}
