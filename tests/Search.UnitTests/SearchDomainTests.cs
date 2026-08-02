using System;
using Search.Domain.Entities;
using Moq;
using Shouldly;
using Xunit;

namespace Search.UnitTests;

public class SearchDomainTests
{
    [Fact]
    public void SearchIndex_Initialization_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var sku = "SEARCH-001";
        var name = "Wireless Mouse";
        var price = 29.99m;

        // Act
        var index = new SearchIndex
        {
            Sku = sku,
            Name = name,
            Description = "Ergonomic wireless mouse",
            Price = price
        };

        // Assert
        index.Sku.ShouldBe(sku);
        index.Name.ShouldBe(name);
        index.Price.ShouldBe(price);
        index.UpdatedAt.ShouldNotBe(default);
    }

    [Fact]
    public void MockSearchService_UsingMoq_ShouldBeSupported()
    {
        // Arrange
        var mockService = new Mock<IDisposable>();
        mockService.Setup(s => s.Dispose());

        // Act
        mockService.Object.Dispose();

        // Assert
        mockService.Verify(s => s.Dispose(), Times.Once);
    }
}
