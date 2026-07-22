using Inventory.Domain.Entities;
using Inventory.Application.Inventory.Commands;
using Shouldly;
using Xunit;

namespace Inventory.UnitTests;

public class InventoryTests
{
    [Fact]
    public void Stock_Reserve_Should_Return_True_When_Sufficient_Quantity()
    {
        var stock = new Stock("SKU-1", 10);
        var result = stock.Reserve(5);
        result.ShouldBeTrue();
        stock.ReservedQuantity.ShouldBe(5);
        stock.AvailableQuantity.ShouldBe(5);
    }

    [Fact]
    public void Stock_Reserve_Should_Return_False_When_Insufficient_Quantity()
    {
        var stock = new Stock("SKU-2", 2);
        var result = stock.Reserve(5);
        result.ShouldBeFalse();
        stock.ReservedQuantity.ShouldBe(0);
    }

    [Fact]
    public void GetStockAvailabilityQuery_Should_Hold_Correct_Sku()
    {
        var query = new GetStockAvailabilityQuery("SKU-1");
        query.Sku.ShouldBe("SKU-1");
    }
}
