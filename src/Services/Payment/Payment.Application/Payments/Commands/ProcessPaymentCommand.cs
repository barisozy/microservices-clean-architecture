using ECommerce.Contracts.Events.v1;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payment.Application.Common.Interfaces;
using Payment.Domain.Entities;

namespace Payment.Application.Payment.Commands;

public record ProcessPaymentCommand(Guid OrderId, string IdempotencyKey, decimal Amount, List<OrderItemContractDto> Items, DateTimeOffset OrderCreatedAt) : IRequest<Guid>;

public class ProcessPaymentCommandHandler(IPaymentDbContext context, IPublishEndpoint publishEndpoint, IPaymentReadRepository paymentReadRepository) : IRequestHandler<ProcessPaymentCommand, Guid>
{
    public async Task<Guid> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        var existingPayment = await context.Payment.FirstOrDefaultAsync(p => p.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existingPayment != null) return existingPayment.Id;

        var payment = PaymentRecord.Create(request.OrderId, request.IdempotencyKey, request.Amount);
        bool shouldFail = request.Items.Any(i => i.Sku.Contains("FAIL_PAYMENT"));

        if (shouldFail)
        {
            payment.Fail("Card declined or insufficient funds (Simulated).");
            context.Payment.Add(payment);
            
            await publishEndpoint.Publish(new PaymentFailed(payment.OrderId, payment.IdempotencyKey, "Simulated payment failure.", DateTimeOffset.UtcNow), cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            
            await paymentReadRepository.SetPaymenttatusAsync(payment.OrderId, payment.Status.ToString(), cancellationToken);
        }
        else
        {
            payment.Complete();
            context.Payment.Add(payment);
            
            await publishEndpoint.Publish(new PaymentCompleted(payment.OrderId, payment.Id, payment.IdempotencyKey, DateTimeOffset.UtcNow, request.OrderCreatedAt), cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            
            await paymentReadRepository.SetPaymenttatusAsync(payment.OrderId, payment.Status.ToString(), cancellationToken);
        }

        return payment.Id;
    }
}
