using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Ordering.Api.Infrastructure;
using Ordering.Application.Basket.Commands;
using System.Security.Claims;

namespace Ordering.Api.Endpoints;

public class BasketEndpoints : IEndpointGroup
{
    public void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/basket")
                       .RequireAuthorization();

        group.MapDelete("/", DeleteBasket);
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
