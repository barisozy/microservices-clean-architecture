using ECommerce.Contracts.Events.v1;
using IAM.Application;
using IAM.Domain.Entities;
using IAM.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace IAM.Infrastructure.Identity;

public sealed class PermissionEvaluator(
    IamDbContext dbContext,
    IDistributedCache cache,
    IPublishEndpoint publishEndpoint) : IPermissionEvaluator
{
    public async Task<PermissionResult> CheckAsync(string subject, string permission, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(subject, out var subjectGuid))
        {
            await PublishDeniedAsync(subject, permission, cancellationToken);
            return new PermissionResult(false, "GUEST");
        }

        var cacheKey = $"perm:{subjectGuid:D}:{permission}";
        var cached = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var parts = cached.Split('|', 2);
            var allowed = parts[0] == "1";
            if (!allowed) await PublishDeniedAsync(subject, permission, cancellationToken);
            return new PermissionResult(allowed, parts.Length == 2 ? parts[1] : "GUEST");
        }

        var profile = await dbContext.Profiles.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.KeycloakSubject == subjectGuid, cancellationToken);
        var role = profile?.Role?.ToUpperInvariant() ?? "GUEST";
        var granted = profile is { Status: IamProfileStatus.Active }
            && (role == "ADMIN" || role == "CUSTOMER" && permission is "Catalog.Read" or "Customer.Profile.Read" or "Customer.Profile.Write");
        if (!granted) await PublishDeniedAsync(subject, permission, cancellationToken);

        await cache.SetStringAsync(
            cacheKey,
            $"{(granted ? "1" : "0")}|{role}",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) },
            cancellationToken);
        return new PermissionResult(granted, role);
    }

    private async Task PublishDeniedAsync(string subject, string permission, CancellationToken cancellationToken)
    {
        await publishEndpoint.Publish(new PermissionDenied(subject, permission, permission, DateTimeOffset.UtcNow), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
