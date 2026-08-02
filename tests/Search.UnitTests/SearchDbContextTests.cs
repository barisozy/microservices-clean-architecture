using Microsoft.EntityFrameworkCore;
using Search.Domain.Entities;
using Search.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace Search.UnitTests;

public class SearchDbContextTests
{
    [Fact]
    public void Model_ShouldUseSkuAsPrimaryKeyAndConfigureGeneratedSearchVector()
    {
        using var db = new SearchDbContext(new DbContextOptionsBuilder<SearchDbContext>()
            .UseNpgsql("Host=localhost;Database=search_test;Username=postgres;Password=postgres").Options);
        var entity = db.Model.FindEntityType(typeof(SearchIndex))!;

        entity.FindPrimaryKey()!.Properties.Single().Name.ShouldBe(nameof(SearchIndex.Sku));
        var vector = entity.FindProperty("SearchVector")!;
        vector.ClrType.Name.ShouldBe("NpgsqlTsVector");
        entity.GetIndexes().ShouldContain(index =>
            index.Properties.Single().Name == "SearchVector" && index.GetMethod() == "GIN");
    }
}
