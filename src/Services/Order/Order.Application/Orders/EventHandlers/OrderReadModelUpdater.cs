using MediatR;
using Order.Application.Common.Interfaces;
using Order.Application.Orders.Queries;
using Order.Domain.Events;

namespace Order.Application.Orders.EventHandlers;

public class OrderReadModelUpdater(IOrderReadRepository readRepository) : 
    INotificationHandler<OrderCreatedDomainEvent>,
    INotificationHandler<OrderCancelledDomainEvent>
{
    public async Task Handle(OrderCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        var dto = new OrderStatusDto(
            notification.Order.Id,
            notification.Order.Status.ToString(),
            notification.Order.BuyerId);

        await readRepository.SetOrderAsync(dto, cancellationToken);
    }

    public async Task Handle(OrderCancelledDomainEvent notification, CancellationToken cancellationToken)
    {
        var dto = new OrderStatusDto(
            notification.Order.Id,
            notification.Order.Status.ToString(), // Will be Cancelled
            notification.Order.BuyerId);

        await readRepository.SetOrderAsync(dto, cancellationToken);
    }
}

