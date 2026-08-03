using FluentValidation;
using IAM.Domain.Entities;
using MediatR;

namespace IAM.Application;

public sealed record GetUsersQuery : IRequest<IReadOnlyList<IamProfile>>;
public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>;
public sealed class GetUsersQueryHandler(IIamRepository repository) : IRequestHandler<GetUsersQuery, IReadOnlyList<IamProfile>>
{
    public Task<IReadOnlyList<IamProfile>> Handle(GetUsersQuery request, CancellationToken cancellationToken) => repository.GetUsersAsync(cancellationToken);
}
