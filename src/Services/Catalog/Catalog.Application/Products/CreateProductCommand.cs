using Catalog.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Catalog.Application;

public sealed record CreateProductCommand(string Sku, string Name, string Description, decimal Price, Guid BrandId, Guid CategoryId) : IRequest<Product>;
public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(request => request.Sku).NotEmpty().MaximumLength(100);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Description).MaximumLength(4000);
        RuleFor(request => request.Price).GreaterThanOrEqualTo(0);
    }
}
public sealed class CreateProductCommandHandler(ICatalogRepository repository) : IRequestHandler<CreateProductCommand, Product>
{
    public Task<Product> Handle(CreateProductCommand request, CancellationToken cancellationToken) => repository.CreateProductAsync(request, cancellationToken);
}
