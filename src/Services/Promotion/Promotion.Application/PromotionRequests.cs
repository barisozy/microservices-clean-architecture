using FluentValidation;
using MediatR;
using Promotion.Domain.Entities;

namespace Promotion.Application;

public interface IPromotionRepository
{
    Task<IReadOnlyList<Coupon>> GetCouponsAsync(CancellationToken cancellationToken);
    Task<Coupon> CreateCouponAsync(CreateCouponCommand command, CancellationToken cancellationToken);
    Task<Coupon?> GetCouponAsync(string code, CancellationToken cancellationToken);
}

public sealed record GetCouponsQuery : IRequest<IReadOnlyList<Coupon>>;
public sealed record CreateCouponCommand(
    string Code,
    string DiscountType,
    decimal Value,
    DateTime ExpiresAt,
    string Actor,
    bool PublishAuditEvent) : IRequest<Coupon>;
public sealed record ApplyCouponQuery(string Code, decimal OrderTotal) : IRequest<ApplyCouponResult>;
public sealed record ApplyCouponResult(decimal DiscountedTotal, bool IsValid, string Message);

public sealed class GetCouponsQueryValidator : AbstractValidator<GetCouponsQuery>;

public sealed class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(100);
        RuleFor(request => request.DiscountType).Must(value =>
            value.Equals("PERCENTAGE", StringComparison.OrdinalIgnoreCase)
            || value.Equals("FIXED", StringComparison.OrdinalIgnoreCase));
        RuleFor(request => request.Value).GreaterThan(0);
        RuleFor(request => request.ExpiresAt).GreaterThan(DateTime.UtcNow);
        RuleFor(request => request.Actor).NotEmpty();
    }
}

public sealed class ApplyCouponQueryValidator : AbstractValidator<ApplyCouponQuery>
{
    public ApplyCouponQueryValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(100);
        RuleFor(request => request.OrderTotal).GreaterThanOrEqualTo(0);
    }
}

public sealed class GetCouponsQueryHandler(IPromotionRepository repository)
    : IRequestHandler<GetCouponsQuery, IReadOnlyList<Coupon>>
{
    public Task<IReadOnlyList<Coupon>> Handle(GetCouponsQuery request, CancellationToken cancellationToken) => repository.GetCouponsAsync(cancellationToken);
}

public sealed class CreateCouponCommandHandler(IPromotionRepository repository)
    : IRequestHandler<CreateCouponCommand, Coupon>
{
    public Task<Coupon> Handle(CreateCouponCommand request, CancellationToken cancellationToken) => repository.CreateCouponAsync(request, cancellationToken);
}

public sealed class ApplyCouponQueryHandler(IPromotionRepository repository)
    : IRequestHandler<ApplyCouponQuery, ApplyCouponResult>
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
