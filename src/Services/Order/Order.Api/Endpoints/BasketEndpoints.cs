using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Order.Api.Infrastructure;
using Order.Application.Basket.Commands;
using System.Security.Claims;

namespace Order.Api.Endpoints;

public class BasketEndpoints : IEndpointGroup
{
    public void Map(WebApplication app)
    {
        var prefix = app.Services.GetService<Asp.Versioning.IApiVersionParser>() is null
            ? "/api/v1"
            : "/api/v{version:apiVersion}";
        var group = app.MapGroup($"{prefix}/basket")
                       .RequireAuthorization();

        group.MapGet("/", GetBasket);
        group.MapPut("/", UpdateBasket);
        group.MapDelete("/", DeleteBasket);
    }

    /// <summary>GET /api/v1/basket — Read basket items for the authenticated user</summary>
    private static async Task<Ok<Dictionary<string, int>>> GetBasket(
        HttpContext httpContext,
        ISender sender)
    {
        var buyerId = httpContext.User.FindFirstValue("sub")
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "anonymous";
        var result = await sender.Send(new GetBasketQuery(buyerId));
        return TypedResults.Ok(result);
    }

    /// <summary>PUT /api/v1/basket — Replace basket items (sliding 7-day TTL refreshed on every write)</summary>
    private static async Task<Ok<bool>> UpdateBasket(
        HttpContext httpContext,
        ISender sender,
        List<UpdateBasketItemDto> items)
    {
        var buyerId = httpContext.User.FindFirstValue("sub")
            ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "anonymous";
        var result = await sender.Send(new UpdateBasketCommand(buyerId, items));
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<bool>, BadRequest>> DeleteBasket(
        HttpContext httpContext,
        ISender sender)
    {
        var buyerId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var result = await sender.Send(new DeleteBasketCommand(buyerId));
        return TypedResults.Ok(result);
    }
}
