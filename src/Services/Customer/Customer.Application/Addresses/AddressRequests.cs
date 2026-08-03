using Customer.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Customer.Application;

public sealed record GetAddressesQuery(Guid Subject) : IRequest<IReadOnlyList<Address>>;
public sealed record CreateAddressCommand(Guid Subject, string Line1, string City, string PostalCode) : IRequest<Address>;
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
public sealed class GetAddressesQueryHandler(ICustomerRepository repository) : IRequestHandler<GetAddressesQuery, IReadOnlyList<Address>>
{
    public Task<IReadOnlyList<Address>> Handle(GetAddressesQuery request, CancellationToken cancellationToken) => repository.GetAddressesAsync(request.Subject, cancellationToken);
}
public sealed class CreateAddressCommandHandler(ICustomerRepository repository) : IRequestHandler<CreateAddressCommand, Address>
{
    public Task<Address> Handle(CreateAddressCommand request, CancellationToken cancellationToken) => repository.AddAddressAsync(request.Subject, request.Line1, request.City, request.PostalCode, cancellationToken);
}
