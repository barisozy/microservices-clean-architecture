using ECommerce.Contracts.Events.v1;
using IAM.Application;
using IAM.Application.Common.Interfaces;
using IAM.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IAM.Infrastructure.Data;

public sealed class IamRepository(
    IamDbContext dbContext,
    IKeycloakAdminClient keycloak,
    IPublishEndpoint publishEndpoint,
    ILogger<IamRepository> logger) : IIamRepository
{
    public async Task<IReadOnlyList<IamProfile>> GetUsersAsync(CancellationToken cancellationToken) =>
        await dbContext.Profiles.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<CreateUserResult> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var profile = new IamProfile
        {
            KeycloakSubject = command.Subject,
            DisplayName = command.DisplayName,
            Email = command.Email,
            Role = command.Role.ToUpperInvariant(),
            Status = IamProfileStatus.PendingIdentity
        };
        dbContext.Profiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (command.SkipExternalProvisioning)
        {
            profile.Status = IamProfileStatus.Active;
            await publishEndpoint.Publish(new UserRegistered(profile.KeycloakSubject, profile.Email), cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new CreateUserResult(profile, false);
        }

        try
        {
            await keycloak.EnsureUserExistsAsync(profile, cancellationToken);
            profile.Status = IamProfileStatus.Active;
            var provisionedAt = DateTimeOffset.UtcNow;
            await publishEndpoint.Publish(new UserRegistered(profile.KeycloakSubject, profile.Email), cancellationToken);
            await publishEndpoint.Publish(new UserProvisioned(profile.KeycloakSubject, profile.Email, provisionedAt), cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new CreateUserResult(profile, false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Keycloak provisioning deferred for subject {Subject}", profile.KeycloakSubject);
            return new CreateUserResult(profile, true);
        }
    }

    public async Task<Invitation> CreateInvitationAsync(CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Invitations.FirstOrDefaultAsync(
            invitation => invitation.IdempotencyKey == command.IdempotencyKey,
            cancellationToken);
        if (existing is not null) return existing;

        var invitation = new Invitation
        {
            IdempotencyKey = command.IdempotencyKey,
            Email = command.Email,
            Role = command.Role.ToUpperInvariant(),
            ExpiresAt = command.ExpiresAt
        };
        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return invitation;
    }

    public async Task<IReadOnlyList<GroupMembership>> GetGroupsAsync(CancellationToken cancellationToken) =>
        await dbContext.GroupMemberships.AsNoTracking().ToListAsync(cancellationToken);
}
