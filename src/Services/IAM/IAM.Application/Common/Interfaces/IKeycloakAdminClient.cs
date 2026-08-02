using IAM.Domain.Entities;

namespace IAM.Application.Common.Interfaces;

public interface IKeycloakAdminClient
{
    /// <summary>
    /// Ensures the profile has exactly one matching Keycloak identity. The
    /// operation is safe to retry after an ambiguous network failure.
    /// </summary>
    Task EnsureUserExistsAsync(IamProfile profile, CancellationToken cancellationToken = default);
}
