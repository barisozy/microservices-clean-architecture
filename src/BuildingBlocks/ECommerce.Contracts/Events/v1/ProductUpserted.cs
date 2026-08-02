namespace ECommerce.Contracts.Events.v1;

public record ProductUpserted(string Sku, string Name, decimal Price);
