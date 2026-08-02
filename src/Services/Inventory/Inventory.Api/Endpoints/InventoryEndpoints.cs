using Inventory.Api.Infrastructure;
using Inventory.Application.Inventory.Commands;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OutputCaching;
using System.Security.Claims;

namespace Inventory.Api.Endpoints;

public class InventoryEndpoints : IEndpointGroup
{
    public void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/inventory");

        group.MapGet("/{sku}/availability", GetAvailability)
             .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(5)).SetVaryByRouteValue("sku"));

        group.MapPut("/{sku}/stock", SetStock)
            .RequireAuthorization(policy => policy.RequireRole("ADMIN"));
    }

    private static async Task<Ok<int>> GetAvailability(
        string sku,
        ISender sender)
    {
        var availability = await sender.Send(new GetStockAvailabilityQuery(sku));
        return TypedResults.Ok(availability);
    }

    private static async Task<Ok<int>> SetStock(
        string sku,
        SetStockRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var availability = await sender.Send(
            new SetStockCommand(sku, request.Quantity),
            cancellationToken);
        return TypedResults.Ok(availability);
    }
}

public sealed record SetStockRequest(int Quantity);
