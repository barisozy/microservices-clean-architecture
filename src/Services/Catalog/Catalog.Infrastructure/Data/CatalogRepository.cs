using Catalog.Application;
using Catalog.Domain.Entities;
using ECommerce.Contracts.Events.v1;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure.Data;

public sealed class CatalogRepository(CatalogDbContext dbContext, IPublishEndpoint publishEndpoint) : ICatalogRepository
{
    public async Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken cancellationToken) =>
        await dbContext.Products.AsNoTracking().ToListAsync(cancellationToken);

    public Task<Product?> GetProductAsync(string sku, CancellationToken cancellationToken) =>
        dbContext.Products.AsNoTracking().FirstOrDefaultAsync(product => product.Sku == sku, cancellationToken);

    public async Task<Product> CreateProductAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Sku = command.Sku,
            Name = command.Name,
            Description = command.Description,
            Price = command.Price,
            BrandId = command.BrandId,
            CategoryId = command.CategoryId
        };
        dbContext.Products.Add(product);
        await publishEndpoint.Publish(new ProductUpserted(product.Sku, product.Name, product.Price), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return product;
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        await dbContext.Categories.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Brand>> GetBrandsAsync(CancellationToken cancellationToken) =>
        await dbContext.Brands.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductVariant>?> GetVariantsAsync(string sku, CancellationToken cancellationToken)
    {
        var productId = await dbContext.Products.AsNoTracking()
            .Where(product => product.Sku == sku)
            .Select(product => (Guid?)product.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return productId is null
            ? null
            : await dbContext.Variants.AsNoTracking().Where(variant => variant.ProductId == productId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductImage>?> GetImagesAsync(string sku, CancellationToken cancellationToken)
    {
        var productId = await dbContext.Products.AsNoTracking()
            .Where(product => product.Sku == sku)
            .Select(product => (Guid?)product.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return productId is null
            ? null
            : await dbContext.Images.AsNoTracking().Where(image => image.ProductId == productId).ToListAsync(cancellationToken);
    }
}
