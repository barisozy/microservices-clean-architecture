using FluentValidation;

namespace Inventory.Application.Inventory.Commands;

public sealed class SetStockCommandValidator : AbstractValidator<SetStockCommand>
{
    public SetStockCommandValidator()
    {
        RuleFor(command => command.Sku).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Quantity).InclusiveBetween(0, 10_000_000);
    }
}

public sealed class ReserveStockCommandValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.Sku).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Quantity).InclusiveBetween(1, 1_000);
    }
}
