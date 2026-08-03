using IAM.Domain.Entities;

namespace IAM.Application;

public interface IIamRepository
{
    Task<IReadOnlyList<IamProfile>> GetUsersAsync(CancellationToken cancellationToken);
    Task<CreateUserResult> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken);
    Task<Invitation> CreateInvitationAsync(CreateInvitationCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<GroupMembership>> GetGroupsAsync(CancellationToken cancellationToken);
}

public interface IPermissionEvaluator
{
    Task<PermissionResult> CheckAsync(string subject, string permission, CancellationToken cancellationToken);
}
