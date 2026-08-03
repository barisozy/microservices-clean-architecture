namespace Catalog.Domain.Entities;

public sealed class Brand
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;
}
