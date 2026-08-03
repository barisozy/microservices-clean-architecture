using Customer.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Customer.Application;

public sealed record UpdateProfileCommand(Guid Subject, string DisplayName, string Email) : IRequest<CustomerProfile>;
public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(request => request.Subject).NotEmpty();
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}
public sealed class UpdateProfileCommandHandler(ICustomerRepository repository) : IRequestHandler<UpdateProfileCommand, CustomerProfile>
{
    public Task<CustomerProfile> Handle(UpdateProfileCommand request, CancellationToken cancellationToken) => repository.UpsertProfileAsync(request.Subject, request.DisplayName, request.Email, cancellationToken);
}
