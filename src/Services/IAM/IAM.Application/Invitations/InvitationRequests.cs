using FluentValidation;
using IAM.Domain.Entities;
using MediatR;

namespace IAM.Application;

public sealed record CreateInvitationCommand(Guid IdempotencyKey, string Email, string Role, DateTime ExpiresAt) : IRequest<Invitation>;
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
public sealed class CreateInvitationCommandHandler(IIamRepository repository) : IRequestHandler<CreateInvitationCommand, Invitation>
{
    public Task<Invitation> Handle(CreateInvitationCommand request, CancellationToken cancellationToken) => repository.CreateInvitationAsync(request, cancellationToken);
}
