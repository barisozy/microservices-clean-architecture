namespace Catalog.Domain.Entities;

public class ProductVariant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string AttributesJson { get; set; } = "{}";
}

public class ProductImage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProductId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
