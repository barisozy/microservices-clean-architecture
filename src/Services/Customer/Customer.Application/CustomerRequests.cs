using Customer.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Customer.Application;

public interface ICustomerRepository
{
    Task<CustomerProfile?> GetProfileAsync(Guid subject, CancellationToken cancellationToken);
    Task<CustomerProfile> UpsertProfileAsync(Guid subject, string displayName, string email, CancellationToken cancellationToken);
    Task<IReadOnlyList<Address>> GetAddressesAsync(Guid subject, CancellationToken cancellationToken);
    Task<Address> AddAddressAsync(Guid subject, string line1, string city, string postalCode, CancellationToken cancellationToken);
}

public sealed record GetProfileQuery(Guid Subject) : IRequest<CustomerProfile?>;
public sealed record UpdateProfileCommand(Guid Subject, string DisplayName, string Email) : IRequest<CustomerProfile>;
public sealed record GetAddressesQuery(Guid Subject) : IRequest<IReadOnlyList<Address>>;
public sealed record CreateAddressCommand(Guid Subject, string Line1, string City, string PostalCode) : IRequest<Address>;

public sealed class GetProfileQueryValidator : AbstractValidator<GetProfileQuery>
{
    public GetProfileQueryValidator() => RuleFor(request => request.Subject).NotEmpty();
}

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(request => request.Subject).NotEmpty();
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public sealed class GetAddressesQueryValidator : AbstractValidator<GetAddressesQuery>
{
    public GetAddressesQueryValidator() => RuleFor(request => request.Subject).NotEmpty();
}

public sealed class CreateAddressCommandValidator : AbstractValidator<CreateAddressCommand>
{
    public CreateAddressCommandValidator()
    {
        RuleFor(request => request.Subject).NotEmpty();
        RuleFor(request => request.Line1).NotEmpty().MaximumLength(300);
        RuleFor(request => request.City).NotEmpty().MaximumLength(100);
        RuleFor(request => request.PostalCode).NotEmpty().MaximumLength(20);
    }
}

public sealed class GetProfileQueryHandler(ICustomerRepository repository)
    : IRequestHandler<GetProfileQuery, CustomerProfile?>
{
    public Task<CustomerProfile?> Handle(GetProfileQuery request, CancellationToken cancellationToken) =>
        repository.GetProfileAsync(request.Subject, cancellationToken);
}

public sealed class UpdateProfileCommandHandler(ICustomerRepository repository)
    : IRequestHandler<UpdateProfileCommand, CustomerProfile>
{
    public Task<CustomerProfile> Handle(UpdateProfileCommand request, CancellationToken cancellationToken) =>
        repository.UpsertProfileAsync(request.Subject, request.DisplayName, request.Email, cancellationToken);
}

public sealed class GetAddressesQueryHandler(ICustomerRepository repository)
    : IRequestHandler<GetAddressesQuery, IReadOnlyList<Address>>
{
    public Task<IReadOnlyList<Address>> Handle(GetAddressesQuery request, CancellationToken cancellationToken) =>
        repository.GetAddressesAsync(request.Subject, cancellationToken);
}

public sealed class CreateAddressCommandHandler(ICustomerRepository repository)
    : IRequestHandler<CreateAddressCommand, Address>
{
    public Task<Address> Handle(CreateAddressCommand request, CancellationToken cancellationToken) =>
        repository.AddAddressAsync(request.Subject, request.Line1, request.City, request.PostalCode, cancellationToken);
}
