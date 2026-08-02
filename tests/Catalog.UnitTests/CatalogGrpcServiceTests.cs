using Catalog.Api.Services;
using Catalog.Domain.Entities;
using Catalog.Infrastructure.Data;
using ECommerce.Contracts.Protos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Catalog.UnitTests;

public class CatalogGrpcServiceTests
{
    [Fact]
    public async Task GetPriceSnapshot_ShouldReturnAvailableProductOrUnavailableResponse()
    {
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Products.Add(new Product { Sku = "SKU-1", Name = "Widget", Price = 19.95m });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new CatalogGrpcService(db, NullLogger<CatalogGrpcService>.Instance);

        var found = await service.GetPriceSnapshot(new GetPriceSnapshotRequest { Sku = "SKU-1" }, null!);
        var missing = await service.GetPriceSnapshot(new GetPriceSnapshotRequest { Sku = "missing" }, null!);

        found.Available.ShouldBeTrue();
        found.UnitPrice.ShouldBe(19.95d);
        missing.Available.ShouldBeFalse();
        missing.UnitPrice.ShouldBe(0d);
    }

    [Fact]
    public async Task GetPriceSnapshot_ShouldReturnTheExactStoredDecimalPrice()
    {
        await using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Products.Add(new Product { Sku = "PRECISION", Price = 12.34m });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new CatalogGrpcService(db, NullLogger<CatalogGrpcService>.Instance);
        var response = await service.GetPriceSnapshot(
            new GetPriceSnapshotRequest { Sku = "PRECISION" }, null!);

        response.Available.ShouldBeTrue();
        response.UnitPrice.ShouldBe(12.34d);
    }
}
