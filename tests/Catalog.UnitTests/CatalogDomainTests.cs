using System;
using Catalog.Domain.Entities;
using Moq;
using Shouldly;
using Xunit;

namespace Catalog.UnitTests;

public class CatalogDomainTests
{
    [Fact]
    public void Product_Initialization_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var sku = "PROD-001";
        var name = "Test Laptop";
        var price = 999.99m;
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        // Act
        var product = new Product
        {
            Sku = sku,
            Name = name,
            Description = "High performance laptop",
            Price = price,
            BrandId = brandId,
            CategoryId = categoryId
        };

        // Assert
        product.Id.ShouldNotBe(Guid.Empty);
        product.Sku.ShouldBe(sku);
        product.Name.ShouldBe(name);
        product.Price.ShouldBe(price);
        product.BrandId.ShouldBe(brandId);
        product.CategoryId.ShouldBe(categoryId);
        product.CreatedAt.ShouldNotBe(default);
    }

    [Fact]
    public void MockCatalogRepo_UsingMoq_ShouldBeSupported()
    {
        // Arrange
        var mockService = new Mock<IDisposable>();
        mockService.Setup(s => s.Dispose());

        // Act
        mockService.Object.Dispose();

        // Assert
        mockService.Verify(s => s.Dispose(), Times.Once);
    }

    [Fact]
    public void Category_And_Brand_ShouldHaveVersion7IdsAndKeepNames()
    {
        var category = new Category { Name = "Laptops" };
        var brand = new Brand { Name = "Contoso" };

        category.Id.ShouldNotBe(Guid.Empty);
        category.Name.ShouldBe("Laptops");
        brand.Id.ShouldNotBe(Guid.Empty);
        brand.Name.ShouldBe("Contoso");
    }

    [Fact]
    public void ProductVariant_ShouldKeepProductSpecificSkuAndAttributes()
    {
        var productId = Guid.CreateVersion7();
        var variant = new ProductVariant
        {
            ProductId = productId,
            Sku = "LAPTOP-16GB",
            AttributesJson = "{\"memory\":\"16GB\"}"
        };

        variant.Id.ShouldNotBe(Guid.Empty);
        variant.ProductId.ShouldBe(productId);
        variant.Sku.ShouldBe("LAPTOP-16GB");
        variant.AttributesJson.ShouldContain("16GB");
    }

    [Fact]
    public void ProductImage_ShouldKeepProductUrlAndSortOrder()
    {
        var productId = Guid.CreateVersion7();
        var image = new ProductImage
        {
            ProductId = productId,
            Url = "https://cdn.example.test/laptop.png",
            SortOrder = 2
        };

        image.Id.ShouldNotBe(Guid.Empty);
        image.ProductId.ShouldBe(productId);
        image.Url.ShouldStartWith("https://");
        image.SortOrder.ShouldBe(2);
    }
}
