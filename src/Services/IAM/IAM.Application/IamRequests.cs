using FluentValidation;
using IAM.Domain.Entities;
using MediatR;

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

public sealed record GetUsersQuery : IRequest<IReadOnlyList<IamProfile>>;
public sealed record GetGroupsQuery : IRequest<IReadOnlyList<GroupMembership>>;
public sealed record CreateUserCommand(
    Guid Subject,
    string DisplayName,
    string Email,
    string Role,
    bool SkipExternalProvisioning) : IRequest<CreateUserResult>;
public sealed record CreateUserResult(IamProfile Profile, bool Accepted);
public sealed record CreateInvitationCommand(
    Guid IdempotencyKey,
    string Email,
    string Role,
    DateTime ExpiresAt) : IRequest<Invitation>;
public sealed record CheckPermissionQuery(string Subject, string Permission) : IRequest<PermissionResult>;
public sealed record PermissionResult(bool Allowed, string Role);

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>;
public sealed class GetGroupsQueryValidator : AbstractValidator<GetGroupsQuery>;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(request => request.Subject).NotEmpty();
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Role).Must(role => role is "CUSTOMER" or "ADMIN");
    }
}

public sealed class CreateInvitationCommandValidator : AbstractValidator<CreateInvitationCommand>
{
    public CreateInvitationCommandValidator()
    {
        RuleFor(request => request.IdempotencyKey).NotEmpty();
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Role).Must(role => role is "CUSTOMER" or "ADMIN");
        RuleFor(request => request.ExpiresAt).GreaterThan(DateTime.UtcNow);
    }
}

public sealed class CheckPermissionQueryValidator : AbstractValidator<CheckPermissionQuery>
{
    public CheckPermissionQueryValidator()
    {
        RuleFor(request => request.Subject).NotEmpty();
        RuleFor(request => request.Permission).NotEmpty().MaximumLength(200);
    }
}

public sealed class GetUsersQueryHandler(IIamRepository repository) : IRequestHandler<GetUsersQuery, IReadOnlyList<IamProfile>>
{
    public Task<IReadOnlyList<IamProfile>> Handle(GetUsersQuery request, CancellationToken cancellationToken) => repository.GetUsersAsync(cancellationToken);
}

public sealed class GetGroupsQueryHandler(IIamRepository repository) : IRequestHandler<GetGroupsQuery, IReadOnlyList<GroupMembership>>
{
    public Task<IReadOnlyList<GroupMembership>> Handle(GetGroupsQuery request, CancellationToken cancellationToken) => repository.GetGroupsAsync(cancellationToken);
}

public sealed class CreateUserCommandHandler(IIamRepository repository) : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    public Task<CreateUserResult> Handle(CreateUserCommand request, CancellationToken cancellationToken) => repository.CreateUserAsync(request, cancellationToken);
}

public sealed class CreateInvitationCommandHandler(IIamRepository repository) : IRequestHandler<CreateInvitationCommand, Invitation>
{
    public Task<Invitation> Handle(CreateInvitationCommand request, CancellationToken cancellationToken) => repository.CreateInvitationAsync(request, cancellationToken);
}

public sealed class CheckPermissionQueryHandler(IPermissionEvaluator evaluator) : IRequestHandler<CheckPermissionQuery, PermissionResult>
{
    public Task<PermissionResult> Handle(CheckPermissionQuery request, CancellationToken cancellationToken) => evaluator.CheckAsync(request.Subject, request.Permission, cancellationToken);
}
