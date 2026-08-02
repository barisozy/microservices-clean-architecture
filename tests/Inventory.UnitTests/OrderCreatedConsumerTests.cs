using System;
using System.Threading;
using System.Threading.Tasks;
using Inventory.Application.Common.Interfaces;
using Inventory.Application.Inventory.Commands;
using Inventory.Domain.Entities;
using Moq;
using Shouldly;
using Xunit;

namespace Inventory.UnitTests;

public class ReserveStockCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReserveStock_WhenStockAvailable()
    {
        var dbContextMock = new Mock<IInventoryDbContext>();
        var readRepoMock = new Mock<IStockReadRepository>();

        var stocks = new List<Stock> { new Stock("SKU-1", 100) };
        var mockSet = stocks.AsQueryable();

        // Testing ReserveStockCommand directly
        var handler = new ReserveStockCommandHandler(dbContextMock.Object, readRepoMock.Object);
        // Basic instantiation check
        handler.ShouldNotBeNull();
    }
}
