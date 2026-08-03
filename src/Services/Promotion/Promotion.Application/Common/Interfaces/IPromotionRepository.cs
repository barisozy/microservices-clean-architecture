using Promotion.Domain.Entities;

namespace Promotion.Application;

public interface IPromotionRepository
{
    Task<IReadOnlyList<Coupon>> GetCouponsAsync(CancellationToken cancellationToken);
    Task<Coupon> CreateCouponAsync(CreateCouponCommand command, CancellationToken cancellationToken);
    Task<Coupon?> GetCouponAsync(string code, CancellationToken cancellationToken);
}
