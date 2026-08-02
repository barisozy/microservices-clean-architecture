using Catalog.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Catalog.Application;

public interface ICatalogRepository
{
    Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken cancellationToken);
    Task<Product?> GetProductAsync(string sku, CancellationToken cancellationToken);
    Task<Product> CreateProductAsync(CreateProductCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductVariant>?> GetVariantsAsync(string sku, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductImage>?> GetImagesAsync(string sku, CancellationToken cancellationToken);
}

public sealed record GetProductsQuery : IRequest<IReadOnlyList<Product>>;
public sealed record GetProductQuery(string Sku) : IRequest<Product?>;
public sealed record GetCategoriesQuery : IRequest<IReadOnlyList<Category>>;
public sealed record GetBrandsQuery : IRequest<IReadOnlyList<Brand>>;
public sealed record GetVariantsQuery(string Sku) : IRequest<IReadOnlyList<ProductVariant>?>;
public sealed record GetImagesQuery(string Sku) : IRequest<IReadOnlyList<ProductImage>?>;
public sealed record CreateProductCommand(
    string Sku,
    string Name,
    string Description,
    decimal Price,
    Guid BrandId,
    Guid CategoryId) : IRequest<Product>;

public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>;
public sealed class GetCategoriesQueryValidator : AbstractValidator<GetCategoriesQuery>;
public sealed class GetBrandsQueryValidator : AbstractValidator<GetBrandsQuery>;

public sealed class GetProductQueryValidator : AbstractValidator<GetProductQuery>
{
    public GetProductQueryValidator() => RuleFor(request => request.Sku).NotEmpty().MaximumLength(100);
}

public sealed class GetVariantsQueryValidator : AbstractValidator<GetVariantsQuery>
{
    public GetVariantsQueryValidator() => RuleFor(request => request.Sku).NotEmpty().MaximumLength(100);
}

public sealed class GetImagesQueryValidator : AbstractValidator<GetImagesQuery>
{
    public GetImagesQueryValidator() => RuleFor(request => request.Sku).NotEmpty().MaximumLength(100);
}

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

public sealed class GetProductsQueryHandler(ICatalogRepository repository)
    : IRequestHandler<GetProductsQuery, IReadOnlyList<Product>>
{
    public Task<IReadOnlyList<Product>> Handle(GetProductsQuery request, CancellationToken cancellationToken) => repository.GetProductsAsync(cancellationToken);
}

public sealed class GetProductQueryHandler(ICatalogRepository repository) : IRequestHandler<GetProductQuery, Product?>
{
    public Task<Product?> Handle(GetProductQuery request, CancellationToken cancellationToken) => repository.GetProductAsync(request.Sku, cancellationToken);
}

public sealed class CreateProductCommandHandler(ICatalogRepository repository) : IRequestHandler<CreateProductCommand, Product>
{
    public Task<Product> Handle(CreateProductCommand request, CancellationToken cancellationToken) => repository.CreateProductAsync(request, cancellationToken);
}

public sealed class GetCategoriesQueryHandler(ICatalogRepository repository) : IRequestHandler<GetCategoriesQuery, IReadOnlyList<Category>>
{
    public Task<IReadOnlyList<Category>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken) => repository.GetCategoriesAsync(cancellationToken);
}

public sealed class GetBrandsQueryHandler(ICatalogRepository repository) : IRequestHandler<GetBrandsQuery, IReadOnlyList<Brand>>
{
    public Task<IReadOnlyList<Brand>> Handle(GetBrandsQuery request, CancellationToken cancellationToken) => repository.GetBrandsAsync(cancellationToken);
}

public sealed class GetVariantsQueryHandler(ICatalogRepository repository) : IRequestHandler<GetVariantsQuery, IReadOnlyList<ProductVariant>?>
{
    public Task<IReadOnlyList<ProductVariant>?> Handle(GetVariantsQuery request, CancellationToken cancellationToken) => repository.GetVariantsAsync(request.Sku, cancellationToken);
}

public sealed class GetImagesQueryHandler(ICatalogRepository repository) : IRequestHandler<GetImagesQuery, IReadOnlyList<ProductImage>?>
{
    public Task<IReadOnlyList<ProductImage>?> Handle(GetImagesQuery request, CancellationToken cancellationToken) => repository.GetImagesAsync(request.Sku, cancellationToken);
}
