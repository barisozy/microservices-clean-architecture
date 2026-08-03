using FluentValidation;
using MediatR;
using Promotion.Application.Common.Interfaces;
using Promotion.Domain.Entities;

namespace Promotion.Application;

public sealed record GetCouponsQuery : IRequest<IReadOnlyList<Coupon>>;
public sealed class GetCouponsQueryValidator : AbstractValidator<GetCouponsQuery>;
public sealed class GetCouponsQueryHandler(IPromotionRepository repository) : IRequestHandler<GetCouponsQuery, IReadOnlyList<Coupon>>
{
    public Task<IReadOnlyList<Coupon>> Handle(GetCouponsQuery request, CancellationToken cancellationToken) => repository.GetCouponsAsync(cancellationToken);
}
