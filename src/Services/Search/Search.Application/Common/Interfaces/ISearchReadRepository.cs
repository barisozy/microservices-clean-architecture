using Search.Domain.Entities;

namespace Search.Application;

public interface ISearchReadRepository
{
    Task<IReadOnlyList<SearchIndex>> SearchAsync(string? query, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> SuggestAsync(string query, CancellationToken cancellationToken);
}
