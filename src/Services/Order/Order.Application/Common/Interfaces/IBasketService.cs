namespace Order.Application.Common.Interfaces;

public interface IBasketService
{
    Task<Dictionary<string, int>> GetBasketAsync(string buyerId, CancellationToken cancellationToken = default);
    Task<bool> SetBasketAsync(string buyerId, Dictionary<string, int> items, CancellationToken cancellationToken = default);
    Task<bool> DeleteBasketAsync(string buyerId, CancellationToken cancellationToken = default);
}
