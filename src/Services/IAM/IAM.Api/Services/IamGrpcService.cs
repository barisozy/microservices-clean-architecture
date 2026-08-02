using ECommerce.Contracts.Protos;
using FluentValidation;
using Grpc.Core;
using IAM.Application;
using MediatR;
using System.Diagnostics.Metrics;

namespace IAM.Api.Services;

public class IamGrpcService : IamService.IamServiceBase
{
    private static readonly Meter Meter = new("IAM.Api");
    private static readonly Histogram<double> PermissionCheckDuration =
        Meter.CreateHistogram<double>("iam.permission_check.duration", "ms");

    private readonly ISender _sender;
    private readonly IValidator<CheckPermissionQuery> _validator;
    private readonly ILogger<IamGrpcService> _logger;
    public IamGrpcService(
        ISender sender,
        IValidator<CheckPermissionQuery> validator,
        ILogger<IamGrpcService> logger)
    {
        _sender = sender;
        _validator = validator;
        _logger = logger;
    }

    public override async Task<CheckPermissionResponse> CheckPermission(CheckPermissionRequest request, ServerCallContext context)
    {
        var startedAt = TimeProvider.System.GetTimestamp();
        _logger.LogInformation("Checking permission {Permission}", request.Permission);

        var query = new CheckPermissionQuery(request.Subject, request.Permission);
        var validation = await _validator.ValidateAsync(query, context?.CancellationToken ?? default);
        if (!validation.IsValid)
        {
            PermissionCheckDuration.Record(TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds);
            return Denied();
        }
        var result = await _sender.Send(query, context?.CancellationToken ?? default);
        PermissionCheckDuration.Record(TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds);
        return new CheckPermissionResponse
        {
            Allowed = result.Allowed,
            Role = result.Role
        };
    }

    private static CheckPermissionResponse Denied() => new() { Allowed = false, Role = "GUEST" };
}
