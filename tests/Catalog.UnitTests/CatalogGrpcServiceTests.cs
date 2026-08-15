using Catalog.Api.Services;
using Catalog.Application;
using Catalog.Domain.Entities;
using ECommerce.Contracts.Protos;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace Catalog.UnitTests;

public class CatalogGrpcServiceTests
{
    [Fact]
    public async Task GetPriceSnapshot_ShouldReturnAvailableProductOrUnavailableResponse()
    {
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(
                It.Is<GetProductQuery>(query => query.Sku == "SKU-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Product { Sku = "SKU-1", Name = "Widget", Price = 19.95m });
        sender.Setup(value => value.Send(
                It.Is<GetProductQuery>(query => query.Sku == "missing"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        var service = new CatalogGrpcService(
            sender.Object,
            new GetProductQueryValidator(),
            NullLogger<CatalogGrpcService>.Instance);

        var found = await service.GetPriceSnapshot(new GetPriceSnapshotRequest { Sku = "SKU-1" }, null!);
        var missing = await service.GetPriceSnapshot(new GetPriceSnapshotRequest { Sku = "missing" }, null!);

        found.Available.ShouldBeTrue();
        found.UnitPrice.MinorUnits.ShouldBe(1995L);
        missing.Available.ShouldBeFalse();
        missing.UnitPrice.MinorUnits.ShouldBe(0L);
    }

    [Fact]
    public async Task GetPriceSnapshot_ShouldReturnTheExactApplicationPrice()
    {
        var sender = new Mock<ISender>();
        sender.Setup(value => value.Send(It.IsAny<GetProductQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Product { Sku = "PRECISION", Price = 12.34m });
        var service = new CatalogGrpcService(sender.Object, new GetProductQueryValidator(), NullLogger<CatalogGrpcService>.Instance);

        var response = await service.GetPriceSnapshot(new GetPriceSnapshotRequest { Sku = "PRECISION" }, null!);

        response.Available.ShouldBeTrue();
        response.UnitPrice.MinorUnits.ShouldBe(1234L);
    }
}
