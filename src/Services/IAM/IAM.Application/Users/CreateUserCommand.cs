using FluentValidation;
using IAM.Domain.Entities;
using MediatR;

namespace IAM.Application;

public sealed record CreateUserCommand(Guid Subject, string DisplayName, string Email, string Role, bool SkipExternalProvisioning) : IRequest<CreateUserResult>;
public sealed record CreateUserResult(IamProfile Profile, bool Accepted);
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
public sealed class CreateUserCommandHandler(IIamRepository repository) : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    public Task<CreateUserResult> Handle(CreateUserCommand request, CancellationToken cancellationToken) => repository.CreateUserAsync(request, cancellationToken);
}
