using Catalog.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Catalog.Application;

public sealed record GetProductsQuery : IRequest<IReadOnlyList<Product>>;
public sealed record GetProductQuery(string Sku) : IRequest<Product?>;
public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>;
public sealed class GetProductQueryValidator : AbstractValidator<GetProductQuery>
{
    public GetProductQueryValidator() => RuleFor(request => request.Sku).NotEmpty().MaximumLength(100);
}
public sealed class GetProductsQueryHandler(ICatalogRepository repository) : IRequestHandler<GetProductsQuery, IReadOnlyList<Product>>
{
    public Task<IReadOnlyList<Product>> Handle(GetProductsQuery request, CancellationToken cancellationToken) => repository.GetProductsAsync(cancellationToken);
}
public sealed class GetProductQueryHandler(ICatalogRepository repository) : IRequestHandler<GetProductQuery, Product?>
{
    public Task<Product?> Handle(GetProductQuery request, CancellationToken cancellationToken) => repository.GetProductAsync(request.Sku, cancellationToken);
}
