using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Ordering.Api.Infrastructure;
using Ordering.Application.Orders.Commands.CreateOrder;
using Ordering.Application.Orders.Queries;
using System.Security.Claims;

namespace Ordering.Api.Endpoints;

/// <summary>
/// POST /api/v1/orders  — Create order (idempotent, Idempotency-Key header required)
/// GET  /api/v1/orders/{orderId} — Get order status
/// </summary>
public class Orders : IEndpointGroup
{
    public void Map(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/orders")
                       .RequireAuthorization();

        group.MapPost("/", CreateOrder)
             .AddEndpointFilter<IdempotencyKeyFilter>();

        group.MapGet("/{orderId:guid}", GetOrder);
    }

    private static async Task<Results<Created<Guid>, Ok<Guid>>> CreateOrder(
        HttpContext httpContext,
        ISender sender,
        CreateOrderRequest request)
    {
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault()
            ?? throw new BadHttpRequestException("Idempotency-Key header is required.");

        var customerId = GetUserId(httpContext);
        var keycloakSubject = GetKeycloakSubject(httpContext);

        var command = new CreateOrderCommand(
            customerId,
            keycloakSubject,
            idempotencyKey,
            request.Items?.Select(i => new OrderItemDto(i.Sku, i.Quantity, i.UnitPrice)).ToList() ?? []
        );

        var id = await sender.Send(command);
        return TypedResults.Created($"/api/v1/orders/{id}", id);
    }

    private static async Task<Results<Ok<OrderStatusDto>, NotFound>> GetOrder(
        ISender sender,
        Guid orderId)
    {
        var result = await sender.Send(new GetOrderQuery(orderId));
        if (result is null) return TypedResults.NotFound();
        return TypedResults.Ok(result);
    }

    private static Guid GetUserId(HttpContext ctx)
    {
        var sub = ctx.User.FindFirstValue("sub")
            ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Guid.Empty.ToString();
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private static Guid GetKeycloakSubject(HttpContext ctx)
    {
        var sub = ctx.User.FindFirstValue("sub")
            ?? Guid.Empty.ToString();
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}

/// <summary>
/// Request DTO — Items is optional: empty = checkout-from-basket mode.
/// </summary>
public record CreateOrderRequest(List<OrderItemRequest>? Items);
public record OrderItemRequest(string Sku, int Quantity, decimal UnitPrice);

