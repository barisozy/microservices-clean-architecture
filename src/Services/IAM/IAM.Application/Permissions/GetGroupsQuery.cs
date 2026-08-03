using FluentValidation;
using IAM.Domain.Entities;
using MediatR;

namespace IAM.Application;

public sealed record GetGroupsQuery : IRequest<IReadOnlyList<GroupMembership>>;
public sealed class GetGroupsQueryValidator : AbstractValidator<GetGroupsQuery>;
public sealed class GetGroupsQueryHandler(IIamRepository repository) : IRequestHandler<GetGroupsQuery, IReadOnlyList<GroupMembership>>
{
    public Task<IReadOnlyList<GroupMembership>> Handle(GetGroupsQuery request, CancellationToken cancellationToken) => repository.GetGroupsAsync(cancellationToken);
}
