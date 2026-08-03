using FluentValidation;
using MediatR;
using Promotion.Application.Common.Interfaces;
using Promotion.Domain.Entities;

namespace Promotion.Application;

public sealed record CreateCouponCommand(string Code, string DiscountType, decimal Value, DateTime ExpiresAt, string Actor, bool PublishAuditEvent) : IRequest<Coupon>;
public sealed class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator()
    {
        RuleFor(request => request.Code).NotEmpty().MaximumLength(100);
        RuleFor(request => request.DiscountType).Must(value => value.Equals("PERCENTAGE", StringComparison.OrdinalIgnoreCase) || value.Equals("FIXED", StringComparison.OrdinalIgnoreCase));
        RuleFor(request => request.Value).GreaterThan(0);
        RuleFor(request => request.ExpiresAt).GreaterThan(DateTime.UtcNow);
        RuleFor(request => request.Actor).NotEmpty();
    }
}
public sealed class CreateCouponCommandHandler(IPromotionRepository repository) : IRequestHandler<CreateCouponCommand, Coupon>
{
    public Task<Coupon> Handle(CreateCouponCommand request, CancellationToken cancellationToken) => repository.CreateCouponAsync(request, cancellationToken);
}
