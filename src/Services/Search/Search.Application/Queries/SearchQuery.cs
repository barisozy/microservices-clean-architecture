using FluentValidation;
using MediatR;
using Search.Domain.Entities;

namespace Search.Application;

public sealed record SearchQuery(string? Query) : IRequest<IReadOnlyList<SearchIndex>>;
public sealed class SearchQueryValidator : AbstractValidator<SearchQuery>
{
    public SearchQueryValidator() => RuleFor(request => request.Query).MaximumLength(200);
}
public sealed class SearchQueryHandler(ISearchReadRepository repository) : IRequestHandler<SearchQuery, IReadOnlyList<SearchIndex>>
{
    public Task<IReadOnlyList<SearchIndex>> Handle(SearchQuery request, CancellationToken cancellationToken) => repository.SearchAsync(request.Query, cancellationToken);
}
