using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Ordering.Application.Common.Interfaces;
using Ordering.Domain.Entities;

namespace Ordering.Application.Orders.Commands.CreateOrder;

public record OrderItemDto(string Sku, int Quantity, decimal UnitPrice);

public record CreateOrderCommand(
    Guid CustomerId,
    Guid KeycloakSubject,
    string IdempotencyKey,
    List<OrderItemDto> Items) : IRequest<Guid>;

public class CreateOrderCommandHandler(IOrderingDbContext context, IPublishEndpoint publishEndpoint, StackExchange.Redis.IConnectionMultiplexer redis) : IRequestHandler<CreateOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var key = $"idempotency:order:{request.IdempotencyKey}";
        
        var cachedOrderId = await db.StringGetAsync(key);
        if (!cachedOrderId.IsNullOrEmpty)
        {
            return Guid.Parse(cachedOrderId.ToString());
        }

        var items = request.Items.Select(i => new OrderItem
        {
            Sku = i.Sku,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice
        }).ToList();

        var order = Order.Create(request.CustomerId.ToString(), items);

        context.Orders.Add(order);

        var eventItems = request.Items.Select(i => new OrderItemContractDto(i.Sku, i.Quantity, i.UnitPrice)).ToList();
        var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        await publishEndpoint.Publish(new OrderCreated(order.Id, request.CustomerId, request.IdempotencyKey, eventItems, totalAmount, DateTimeOffset.UtcNow), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        await db.StringSetAsync(key, order.Id.ToString(), TimeSpan.FromDays(1));

        return order.Id;
    }
}
