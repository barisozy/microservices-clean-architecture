using ECommerce.Contracts.Events;
using MassTransit;
using MediatR;
using Payments.Application.Payments.Commands;

namespace Payments.Application.Consumers;

public class OrderCreatedConsumer(ISender sender) : IConsumer<OrderCreatedEvent>
{
    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var msg = context.Message;
        await sender.Send(new ProcessPaymentCommand(msg.OrderId, msg.IdempotencyKey, msg.TotalAmount, msg.Items), context.CancellationToken);
    }
}

