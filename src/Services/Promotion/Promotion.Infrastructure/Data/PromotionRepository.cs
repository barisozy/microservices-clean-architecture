using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Promotion.Application;
using Promotion.Domain.Entities;

namespace Promotion.Infrastructure.Data;

public sealed class PromotionRepository(PromotionDbContext dbContext, IPublishEndpoint publishEndpoint) : IPromotionRepository
{
    public async Task<IReadOnlyList<Coupon>> GetCouponsAsync(CancellationToken cancellationToken) =>
        await dbContext.Coupons.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<Coupon> CreateCouponAsync(CreateCouponCommand command, CancellationToken cancellationToken)
    {
        var coupon = new Coupon
        {
            Code = command.Code,
            DiscountType = command.DiscountType.ToUpperInvariant(),
            Value = command.Value,
            ExpiresAt = command.ExpiresAt
        };
        dbContext.Coupons.Add(coupon);
        if (command.PublishAuditEvent)
        {
            await publishEndpoint.Publish(
                new CouponWritten(command.Actor, coupon.Code, "Created", DateTimeOffset.UtcNow),
                cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return coupon;
    }

    public Task<Coupon?> GetCouponAsync(string code, CancellationToken cancellationToken) =>
        dbContext.Coupons.AsNoTracking().FirstOrDefaultAsync(coupon => coupon.Code == code, cancellationToken);
}
