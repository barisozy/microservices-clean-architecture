using Catalog.Domain.Entities;

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
