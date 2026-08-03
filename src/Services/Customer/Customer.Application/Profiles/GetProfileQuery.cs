using Customer.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Customer.Application;

public sealed record GetProfileQuery(Guid Subject) : IRequest<CustomerProfile?>;
public sealed class GetProfileQueryValidator : AbstractValidator<GetProfileQuery>
{
    public GetProfileQueryValidator() => RuleFor(request => request.Subject).NotEmpty();
}
public sealed class GetProfileQueryHandler(ICustomerRepository repository) : IRequestHandler<GetProfileQuery, CustomerProfile?>
{
    public Task<CustomerProfile?> Handle(GetProfileQuery request, CancellationToken cancellationToken) => repository.GetProfileAsync(request.Subject, cancellationToken);
}
