namespace Catalog.Domain.Entities;

public sealed class ProductVariant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string AttributesJson { get; set; } = "{}";
}
