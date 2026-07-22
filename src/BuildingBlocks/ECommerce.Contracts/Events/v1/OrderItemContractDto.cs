namespace ECommerce.Contracts.Events.v1;

public record OrderItemContractDto(string Sku, int Quantity, decimal UnitPrice);
