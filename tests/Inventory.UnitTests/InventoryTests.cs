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

    [Fact]
    public void Stock_Release_Should_Decrease_ReservedQuantity()
    {
        var stock = new Stock("SKU-1", 10);
        stock.Reserve(5);
        stock.Release(3);
        stock.ReservedQuantity.ShouldBe(2);
        stock.AvailableQuantity.ShouldBe(8);
    }

    [Fact]
    public void Stock_Release_Should_Not_Go_Below_Zero()
    {
        var stock = new Stock("SKU-1", 10);
        stock.Release(5);
        stock.ReservedQuantity.ShouldBe(0);
    }

    [Fact]
    public void InventoryReservation_Create_Should_Initialize_Correctly()
    {
        var orderId = Guid.NewGuid();
        var reservation = InventoryReservation.Create(orderId, "SKU-1", 5);

        reservation.OrderId.ShouldBe(orderId);
        reservation.Sku.ShouldBe("SKU-1");
        reservation.Quantity.ShouldBe(5);
        reservation.IsReleased.ShouldBeFalse();
    }

    [Fact]
    public void InventoryReservation_Release_Should_Set_IsReleased_To_True()
    {
        var reservation = InventoryReservation.Create(Guid.NewGuid(), "SKU-1", 5);
        reservation.Release();
        reservation.IsReleased.ShouldBeTrue();
    }
}
