using ECommerce.Contracts.Events;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payments.Application.Common.Interfaces;
using Payments.Domain.Entities;

namespace Payments.Application.Payments.Commands;

public record ProcessPaymentCommand(Guid OrderId, string IdempotencyKey, decimal Amount, List<OrderItemContractDto> Items) : IRequest<Guid>;

public class ProcessPaymentCommandHandler(IPaymentsDbContext context, IPublishEndpoint publishEndpoint) : IRequestHandler<ProcessPaymentCommand, Guid>
{
    public async Task<Guid> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        var existingPayment = await context.Payments.FirstOrDefaultAsync(p => p.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existingPayment != null) return existingPayment.Id;

        var payment = Payment.Create(request.OrderId, request.IdempotencyKey, request.Amount);
        bool shouldFail = request.Items.Any(i => i.Sku.Contains("FAIL_PAYMENT"));

        if (shouldFail)
        {
            payment.Fail("Card declined or insufficient funds (Simulated).");
            context.Payments.Add(payment);
            await context.SaveChangesAsync(cancellationToken);

            await publishEndpoint.Publish(new PaymentFailedEvent(payment.OrderId, payment.IdempotencyKey, "Simulated payment failure.", DateTimeOffset.UtcNow), cancellationToken);
        }
        else
        {
            payment.Complete();
            context.Payments.Add(payment);
            await context.SaveChangesAsync(cancellationToken);

            await publishEndpoint.Publish(new PaymentCompletedEvent(payment.OrderId, payment.Id, payment.IdempotencyKey, DateTimeOffset.UtcNow), cancellationToken);
        }

        return payment.Id;
    }
}

