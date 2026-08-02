using System.Diagnostics;
using System.Diagnostics.Metrics;
using ECommerce.Contracts.Protos;
using Grpc.Core;
using FluentValidation;
using MediatR;
using Promotion.Application;

namespace Promotion.Api.Services;

public class PromotionGrpcService : PromotionService.PromotionServiceBase
{
    private static readonly Meter Meter = new("Promotion.Api");
    private static readonly Histogram<double> CouponApplyDuration =
        Meter.CreateHistogram<double>("promotion.coupon_apply.duration", "ms");
    private readonly ISender _sender;
    private readonly IValidator<ApplyCouponQuery> _validator;
    private readonly ILogger<PromotionGrpcService> _logger;

    public PromotionGrpcService(
        ISender sender,
        IValidator<ApplyCouponQuery> validator,
        ILogger<PromotionGrpcService> logger)
    {
        _sender = sender;
        _validator = validator;
        _logger = logger;
    }

    public override async Task<ApplyCouponResponse> ApplyCoupon(ApplyCouponRequest request, ServerCallContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;
        _logger.LogInformation("Applying coupon code '{Code}' to order total '{Total}'", request.Code, request.OrderTotal);

        var query = new ApplyCouponQuery(request.Code, (decimal)request.OrderTotal);
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return new ApplyCouponResponse
            {
                DiscountedTotal = request.OrderTotal,
                IsValid = false,
                Message = string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))
            };
        }
        var result = await _sender.Send(query, cancellationToken);
        return new ApplyCouponResponse
        {
            DiscountedTotal = (double)result.DiscountedTotal,
            IsValid = result.IsValid,
            Message = result.Message
        };
        }
        finally
        {
            CouponApplyDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
