using System.Net;
using System.Net.Http.Json;
using Catalog.Domain.Entities;
using Catalog.Infrastructure.Data;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Catalog.UnitTests;

public class CatalogApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ConfigureCatalogHost(builder, "Testing");
    }

    protected static void ConfigureCatalogHost(IWebHostBuilder builder, string environment)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CatalogDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CatalogDbContext>>();
            services.AddDbContext<CatalogDbContext>(options =>
                options.UseInMemoryDatabase("catalog-api-tests"));
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IPublishEndpoint>();
            services.AddSingleton<IPublishEndpoint>(new Mock<IPublishEndpoint>().Object);
        });
    }
}

public sealed class CatalogDevelopmentApiFactory : CatalogApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ConfigureCatalogHost(builder, "Development");
    }
}

public sealed class CatalogApiProgramTests : IClassFixture<CatalogApiFactory>
{
    private readonly CatalogApiFactory _factory;

    public CatalogApiProgramTests(CatalogApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ProductEndpoints_ReturnProductsAndNotFoundForUnknownSku()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            db.Products.RemoveRange(db.Products);
            db.Products.Add(new Product { Id = Guid.NewGuid(), Sku = "API-SKU", Name = "API product", Price = 12.5m });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = _factory.CreateClient();
        var list = await client.GetAsync("/api/v1/catalog/products", TestContext.Current.CancellationToken);
        var found = await client.GetAsync("/api/v1/catalog/products/API-SKU", TestContext.Current.CancellationToken);
        var missing = await client.GetAsync("/api/v1/catalog/products/missing", TestContext.Current.CancellationToken);

        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await list.Content.ReadFromJsonAsync<List<Product>>(TestContext.Current.CancellationToken))!.Count.ShouldBe(1);
        found.StatusCode.ShouldBe(HttpStatusCode.OK);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CatalogLookupEndpoints_ReturnCollections()
    {
        var client = _factory.CreateClient();

        (await client.GetAsync("/api/v1/catalog/categories", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/catalog/brands", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/catalog/products/unknown/variants", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/v1/catalog/products/unknown/images", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProductDetailEndpoints_ReturnVariantsAndImagesForAnExistingProduct()
    {
        const string sku = "DETAIL-SKU";
        var productId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            db.Products.RemoveRange(db.Products);
            db.Variants.RemoveRange(db.Variants);
            db.Images.RemoveRange(db.Images);
            db.Products.Add(new Product { Id = productId, Sku = sku, Name = "Detail product", Price = 10m });
            db.Variants.Add(new ProductVariant { ProductId = productId, Sku = "DETAIL-SKU-BLUE", AttributesJson = "{\"colour\":\"blue\"}" });
            db.Images.Add(new ProductImage { ProductId = productId, Url = "https://cdn.example.test/detail.png", SortOrder = 1 });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = _factory.CreateClient();
        var variants = await client.GetFromJsonAsync<List<ProductVariant>>($"/api/v1/catalog/products/{sku}/variants", TestContext.Current.CancellationToken);
        var images = await client.GetFromJsonAsync<List<ProductImage>>($"/api/v1/catalog/products/{sku}/images", TestContext.Current.CancellationToken);

        variants.ShouldHaveSingleItem().Sku.ShouldBe("DETAIL-SKU-BLUE");
        images.ShouldHaveSingleItem().Url.ShouldBe("https://cdn.example.test/detail.png");
    }

    [Fact]
    public async Task CreateProduct_ReturnsCreatedResourceAndPersistsIt()
    {
        var client = _factory.CreateClient();
        var product = new Product { Id = Guid.Empty, Sku = "NEW-SKU", Name = "New product", Price = 25.75m };

        var response = await client.PostAsJsonAsync("/api/v1/catalog/products", product, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location!.ToString().ShouldBe("/api/v1/catalog/products/NEW-SKU");
        var created = await response.Content.ReadFromJsonAsync<Product>(TestContext.Current.CancellationToken);
        created!.Id.ShouldNotBe(Guid.Empty);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        (await db.Products.SingleAsync(x => x.Sku == "NEW-SKU", TestContext.Current.CancellationToken)).Name.ShouldBe("New product");
    }
}

public sealed class CatalogDevelopmentProgramTests : IClassFixture<CatalogDevelopmentApiFactory>
{
    private readonly CatalogDevelopmentApiFactory _factory;

    public CatalogDevelopmentProgramTests(CatalogDevelopmentApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DevelopmentHost_ExposesOpenApiAndScalarEndpoints()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var scalar = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);
        scalar.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.MovedPermanently);
    }
}
