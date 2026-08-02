using System.Net;
using System.Net.Http.Json;
using Search.Infrastructure.Data;
using Search.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Search.UnitTests;

public sealed class SearchApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SearchDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SearchDbContext>>();
            services.AddDbContext<SearchDbContext>(options => options.UseInMemoryDatabase("search-api-tests"));
            services.Replace(ServiceDescriptor.Scoped<SearchDbContext>(sp =>
                new SearchApiTestDbContext(sp.GetRequiredService<DbContextOptions<SearchDbContext>>())));
            services.RemoveAll<IHostedService>();
        });
    }
}

public sealed class SearchApiTestDbContext(DbContextOptions<SearchDbContext> options) : SearchDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SearchIndex>(entity =>
        {
            entity.HasKey(index => index.Sku);
        });
    }
}

public sealed class SearchApiProgramTests : IClassFixture<SearchApiFactory>
{
    private readonly SearchApiFactory _factory;
    public SearchApiProgramTests(SearchApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SearchEndpoints_HandleEmptyQueries()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/v1/search", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/search?q=", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/search?q=%20%20", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var suggestions = await client.GetAsync("/api/v1/search/suggest", TestContext.Current.CancellationToken);
        suggestions.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await suggestions.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("[]");

        var emptySuggest = await client.GetAsync("/api/v1/search/suggest?q=%20", TestContext.Current.CancellationToken);
        emptySuggest.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await emptySuggest.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("[]");
    }


    [Fact]
    public async Task Search_WhenNoQueryIsSupplied_ReturnsAtMostTwentyDocuments()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SearchDbContext>();
            await db.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

            db.SearchIndices.AddRange(Enumerable.Range(1, 21).Select(number => new SearchIndex
            {
                Sku = $"SKU-{number:D2}",
                Name = $"Product {number}",
                Price = number,
                UpdatedAt = DateTime.UtcNow
            }));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await _factory.CreateClient()
            .GetAsync("/api/v1/search", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var documents = await response.Content.ReadFromJsonAsync<List<SearchIndex>>(TestContext.Current.CancellationToken);
        documents.ShouldNotBeNull();
        documents.Count.ShouldBe(20);
    }
}

public sealed class SearchDevelopmentApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SearchDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SearchDbContext>>();
            services.AddDbContext<SearchDbContext>(options => options.UseInMemoryDatabase("search-dev-tests"));
            services.Replace(ServiceDescriptor.Scoped<SearchDbContext>(sp =>
                new SearchApiTestDbContext(sp.GetRequiredService<DbContextOptions<SearchDbContext>>())));
            services.RemoveAll<IHostedService>();
        });
    }
}

public sealed class SearchDevelopmentProgramTests : IClassFixture<SearchDevelopmentApiFactory>
{
    private readonly SearchDevelopmentApiFactory _factory;
    public SearchDevelopmentProgramTests(SearchDevelopmentApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DevelopmentHost_ExposesOpenApiAndScalarEndpoints()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var scalar = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);
        scalar.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently);
    }
}
