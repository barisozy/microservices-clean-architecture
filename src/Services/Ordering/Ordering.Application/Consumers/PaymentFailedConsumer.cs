using ECommerce.Contracts.Events;
using MassTransit;
using MediatR;
using Ordering.Application.Orders.Commands.CancelOrder;

namespace Ordering.Application.Consumers;

public class PaymentFailedConsumer(ISender sender) : IConsumer<PaymentFailedEvent>
{
    public async Task Consume(ConsumeContext<PaymentFailedEvent> context)
    {
        await sender.Send(new CancelOrderCommand(context.Message.OrderId, context.Message.Reason), context.CancellationToken);
    }
}

