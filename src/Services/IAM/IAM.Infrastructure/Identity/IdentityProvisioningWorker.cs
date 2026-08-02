using ECommerce.Contracts.Events.v1;
using IAM.Application.Common.Interfaces;
using IAM.Domain.Entities;
using IAM.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IAM.Infrastructure.Identity;

public sealed class IdentityProvisioningWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<IdentityProvisioningWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProvisionPendingProfilesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Identity provisioning reconciliation pass failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    internal async Task ProvisionPendingProfilesAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IamDbContext>();
        var keycloak = scope.ServiceProvider.GetRequiredService<IKeycloakAdminClient>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var pendingProfiles = await dbContext.Profiles
            .Where(profile => profile.Status == IamProfileStatus.PendingIdentity)
            .OrderBy(profile => profile.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var profile in pendingProfiles)
        {
            try
            {
                await keycloak.EnsureUserExistsAsync(profile, cancellationToken);
                profile.Status = IamProfileStatus.Active;
                var provisionedAt = DateTimeOffset.UtcNow;
                await publishEndpoint.Publish(
                    new UserRegistered(profile.KeycloakSubject, profile.Email),
                    cancellationToken);
                await publishEndpoint.Publish(
                    new UserProvisioned(profile.KeycloakSubject, profile.Email, provisionedAt),
                    cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Keycloak provisioning remains pending for subject {Subject}",
                    profile.KeycloakSubject);
            }
        }
    }
}
