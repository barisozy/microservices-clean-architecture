using FluentValidation;
using Ordering.Application.Orders.Commands.CancelOrder;
using Ordering.Application.Orders.Commands.CreateOrder;

namespace Ordering.Application.Orders.Commands;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().WithMessage("IdempotencyKey is required.");
    }
}

public class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("OrderId is required.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Reason is required.");
    }
}
