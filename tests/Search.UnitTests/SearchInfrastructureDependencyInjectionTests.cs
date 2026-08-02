using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Search.Infrastructure;
using Search.Infrastructure.Data;
using Shouldly;
using Xunit;

namespace Search.UnitTests;

public class SearchInfrastructureDependencyInjectionTests
{
    [Fact]
    public void AddInfrastructureServices_UsesSearchDbConnectionNameWhenProvided()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SearchDb"] = "Host=search-db;Database=search;Username=test;Password=test"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddInfrastructureServices(configuration);
        using var provider = services.BuildServiceProvider();
        using var db = provider.GetRequiredService<SearchDbContext>();

        db.Database.GetDbConnection().ConnectionString.ShouldContain("Host=search-db");
        provider.GetService<Search.Infrastructure.Consumers.ProductUpsertedConsumer>().ShouldNotBeNull();
    }
}
