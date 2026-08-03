namespace Catalog.Domain.Entities;

public sealed class ProductImage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProductId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
