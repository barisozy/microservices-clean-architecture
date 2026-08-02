using FluentValidation;
using Order.Application.Orders.Commands.CancelOrder;
using Order.Application.Orders.Commands.CreateOrder;

namespace Order.Application.Orders.Commands;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .Must(value => Guid.TryParseExact(value, "D", out var key) && key.ToString("D")[14] == '7')
            .WithMessage("IdempotencyKey must be a canonical UUIDv7 value.");
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.KeycloakSubject).NotEmpty();
        RuleFor(x => x.Items)
            .Must(items => items is null || items.Count <= 100)
            .WithMessage("An order cannot contain more than 100 items.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(value => value.Sku).NotEmpty().MaximumLength(100);
            item.RuleFor(value => value.Quantity).InclusiveBetween(1, 1_000);
            item.RuleFor(value => value.UnitPrice).GreaterThanOrEqualTo(0);
        });
        RuleFor(x => x.CouponCode).MaximumLength(64);
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
