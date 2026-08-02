using System.Diagnostics;
using System.Diagnostics.Metrics;
using ECommerce.Contracts.Protos;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Promotion.Infrastructure.Data;

namespace Promotion.Api.Services;

public class PromotionGrpcService : PromotionService.PromotionServiceBase
{
    private static readonly Meter Meter = new("Promotion.Api");
    private static readonly Histogram<double> CouponApplyDuration =
        Meter.CreateHistogram<double>("promotion.coupon_apply.duration", "ms");
    private readonly PromotionDbContext _dbContext;
    private readonly ILogger<PromotionGrpcService> _logger;

    public PromotionGrpcService(PromotionDbContext dbContext, ILogger<PromotionGrpcService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public override async Task<ApplyCouponResponse> ApplyCoupon(ApplyCouponRequest request, ServerCallContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;
        _logger.LogInformation("Applying coupon code '{Code}' to order total '{Total}'", request.Code, request.OrderTotal);

        var coupon = await _dbContext.Coupons.FirstOrDefaultAsync(
            c => c.Code == request.Code,
            cancellationToken);
        if (coupon == null || coupon.ExpiresAt < DateTime.UtcNow)
        {
            return new ApplyCouponResponse
            {
                DiscountedTotal = request.OrderTotal,
                IsValid = false,
                Message = "Coupon code invalid or expired."
            };
        }

        double discounted = request.OrderTotal;
        if (coupon.DiscountType.Equals("PERCENTAGE", StringComparison.OrdinalIgnoreCase))
        {
            discounted = request.OrderTotal * (1.0 - (double)coupon.Value / 100.0);
        }
        else if (coupon.DiscountType.Equals("FIXED", StringComparison.OrdinalIgnoreCase))
        {
            discounted = Math.Max(0, request.OrderTotal - (double)coupon.Value);
        }

        return new ApplyCouponResponse
        {
            DiscountedTotal = discounted,
            IsValid = true,
            Message = "Coupon applied successfully."
        };
        }
        finally
        {
            CouponApplyDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
