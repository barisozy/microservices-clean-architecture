namespace Ordering.Application.Common.Interfaces;

public interface IUser
{
    string? Id { get; }
}

public interface IBasketService
{
    Task<bool> DeleteBasketAsync(string buyerId, CancellationToken cancellationToken = default);
}
