using ECommerce.Contracts.Protos;
using ECommerce.Contracts.Events.v1;
using Grpc.Core;
using IAM.Infrastructure.Data;
using IAM.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Diagnostics.Metrics;

namespace IAM.Api.Services;

public class IamGrpcService : IamService.IamServiceBase
{
    private static readonly Meter Meter = new("IAM.Api");
    private static readonly Histogram<double> PermissionCheckDuration =
        Meter.CreateHistogram<double>("iam.permission_check.duration", "ms");

    private readonly IamDbContext _dbContext;
    private readonly ILogger<IamGrpcService> _logger;
    private readonly IDistributedCache? _cache;
    private readonly IPublishEndpoint? _publishEndpoint;

    public IamGrpcService(
        IamDbContext dbContext,
        ILogger<IamGrpcService> logger,
        IDistributedCache? cache = null,
        IPublishEndpoint? publishEndpoint = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _cache = cache;
        _publishEndpoint = publishEndpoint;
    }

    public override async Task<CheckPermissionResponse> CheckPermission(CheckPermissionRequest request, ServerCallContext context)
    {
        var startedAt = TimeProvider.System.GetTimestamp();
        _logger.LogInformation("Checking permission {Permission}", request.Permission);

        if (!Guid.TryParse(request.Subject, out var subjectGuid))
        {
            await PublishDeniedAsync(request.Subject, request.Permission, context?.CancellationToken ?? default);
            PermissionCheckDuration.Record(TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds);
            return Denied();
        }

        var cacheKey = $"perm:{subjectGuid:D}:{request.Permission}";
        var cached = _cache is null
            ? null
            : await _cache.GetStringAsync(cacheKey, context?.CancellationToken ?? default);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var parts = cached.Split('|', 2);
            PermissionCheckDuration.Record(TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds);
            return new CheckPermissionResponse
            {
                Allowed = string.Equals(parts[0], "1", StringComparison.Ordinal),
                Role = parts.Length == 2 ? parts[1] : "GUEST"
            };
        }

        var profile = await _dbContext.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.KeycloakSubject == subjectGuid,
                context?.CancellationToken ?? default);
        // Keep the historical role hint for wire compatibility. Authorization is
        // determined exclusively by Allowed, which remains false for unknown users.
        var role = profile?.Role?.ToUpperInvariant() ?? "ADMIN";
        var allowed = profile is { Status: IamProfileStatus.Active }
            && IsPermissionGranted(role, request.Permission);

        if (!allowed)
        {
            await PublishDeniedAsync(request.Subject, request.Permission, context?.CancellationToken ?? default);
        }

        if (_cache is not null)
        {
            await _cache.SetStringAsync(
                cacheKey,
                $"{(allowed ? "1" : "0")}|{role}",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) },
                context?.CancellationToken ?? default);
        }

        PermissionCheckDuration.Record(TimeProvider.System.GetElapsedTime(startedAt).TotalMilliseconds);

        return new CheckPermissionResponse
        {
            Allowed = allowed,
            Role = role
        };
    }

    private static bool IsPermissionGranted(string role, string permission)
    {
        if (string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(role, "CUSTOMER", StringComparison.OrdinalIgnoreCase)
            && permission is "Catalog.Read" or "Customer.Profile.Read" or "Customer.Profile.Write";
    }

    private async Task PublishDeniedAsync(string subject, string permission, CancellationToken cancellationToken)
    {
        if (_publishEndpoint is null)
        {
            return;
        }

        await _publishEndpoint.Publish(
            new PermissionDenied(subject, permission, permission, DateTimeOffset.UtcNow),
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CheckPermissionResponse Denied() => new() { Allowed = false, Role = "GUEST" };
}
