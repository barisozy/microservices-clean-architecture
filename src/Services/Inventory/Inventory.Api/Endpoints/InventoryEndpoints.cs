using Inventory.Api.Infrastructure;
using Inventory.Application.Inventory.Commands;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;

namespace Inventory.Api.Endpoints;

public class InventoryEndpoints : IEndpointGroup
{
    public void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/inventory");

        group.MapGet("/{sku}/availability", GetAvailability)
             .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)).SetVaryByRouteValue("sku"));
    }

    private static async Task<Ok<int>> GetAvailability(
        string sku,
        ISender sender)
    {
        var availability = await sender.Send(new GetStockAvailabilityQuery(sku));
        return TypedResults.Ok(availability);
    }
}
