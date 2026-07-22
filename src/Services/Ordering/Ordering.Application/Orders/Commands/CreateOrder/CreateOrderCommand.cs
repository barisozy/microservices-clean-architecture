using ECommerce.Contracts.Events;
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

public class CreateOrderCommandHandler(IOrderingDbContext context, IPublishEndpoint publishEndpoint) : IRequestHandler<CreateOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = new Order
        {
            BuyerId = request.CustomerId.ToString(),
            OrderItems = request.Items.Select(i => new OrderItem
            {
                Sku = i.Sku,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync(cancellationToken);

        var eventItems = request.Items.Select(i => new OrderItemContractDto(i.Sku, i.Quantity, i.UnitPrice)).ToList();
        var totalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        await publishEndpoint.Publish(new OrderCreatedEvent(order.Id, request.CustomerId, request.IdempotencyKey, eventItems, totalAmount, DateTimeOffset.UtcNow), cancellationToken);

        return order.Id;
    }
}
