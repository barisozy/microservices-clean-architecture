using Catalog.Domain.Entities;
using Catalog.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Catalog.UnitTests;

public class CatalogDbContextTests
{
    [Fact]
    public void Model_ShouldEnforceUniqueProductSkuAndEntityKeys()
    {
        using var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var product = db.Model.FindEntityType(typeof(Product))!;

        product.FindPrimaryKey()!.Properties.Single().Name.ShouldBe(nameof(Product.Id));
        product.GetIndexes().ShouldContain(index => index.Properties.Single().Name == nameof(Product.Sku) && index.IsUnique);
        db.Model.FindEntityType(typeof(ProductVariant)).ShouldNotBeNull();
        db.Model.FindEntityType(typeof(ProductImage)).ShouldNotBeNull();
    }
}
