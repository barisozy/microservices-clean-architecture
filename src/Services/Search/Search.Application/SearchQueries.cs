using FluentValidation;
using MediatR;
using Search.Domain.Entities;

namespace Search.Application;

public interface ISearchReadRepository
{
    Task<IReadOnlyList<SearchIndex>> SearchAsync(string? query, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> SuggestAsync(string query, CancellationToken cancellationToken);
}

public sealed record SearchQuery(string? Query) : IRequest<IReadOnlyList<SearchIndex>>;
public sealed record SuggestQuery(string Query) : IRequest<IReadOnlyList<string>>;

public sealed class SearchQueryValidator : AbstractValidator<SearchQuery>
{
    public SearchQueryValidator() => RuleFor(request => request.Query).MaximumLength(200);
}

public sealed class SuggestQueryValidator : AbstractValidator<SuggestQuery>
{
    public SuggestQueryValidator() => RuleFor(request => request.Query).NotEmpty().MaximumLength(200);
}

public sealed class SearchQueryHandler(ISearchReadRepository repository)
    : IRequestHandler<SearchQuery, IReadOnlyList<SearchIndex>>
{
    public Task<IReadOnlyList<SearchIndex>> Handle(SearchQuery request, CancellationToken cancellationToken) =>
        repository.SearchAsync(request.Query, cancellationToken);
}

public sealed class SuggestQueryHandler(ISearchReadRepository repository)
    : IRequestHandler<SuggestQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> Handle(SuggestQuery request, CancellationToken cancellationToken) =>
        repository.SuggestAsync(request.Query, cancellationToken);
}
