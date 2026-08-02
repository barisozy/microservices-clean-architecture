using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Search.Application;
using Search.Domain.Entities;

namespace Search.Infrastructure.Data;

public sealed class SearchReadRepository(SearchDbContext dbContext) : ISearchReadRepository
{
    public async Task<IReadOnlyList<SearchIndex>> SearchAsync(string? query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await dbContext.SearchIndices.AsNoTracking().Take(20).ToListAsync(cancellationToken);

        var tsQuery = EF.Functions.WebSearchToTsQuery("simple", query.Trim());
        return await dbContext.SearchIndices
            .AsNoTracking()
            .Where(index => EF.Property<NpgsqlTsVector>(index, "SearchVector").Matches(tsQuery))
            .OrderByDescending(index => EF.Property<NpgsqlTsVector>(index, "SearchVector").Rank(tsQuery))
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SuggestAsync(string query, CancellationToken cancellationToken)
    {
        var tsQuery = EF.Functions.WebSearchToTsQuery("simple", query.Trim());
        return await dbContext.SearchIndices
            .AsNoTracking()
            .Where(index => EF.Property<NpgsqlTsVector>(index, "SearchVector").Matches(tsQuery))
            .Select(index => index.Name)
            .Take(5)
            .ToListAsync(cancellationToken);
    }
}
