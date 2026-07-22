namespace Fulfillment.Application.Common.Interfaces;

public interface IFulfillmentReadRepository
{
    Task<string?> GetFulfillmentStatusAsync(Guid orderId, CancellationToken cancellationToken);
    Task SetFulfillmentStatusAsync(Guid orderId, string status, CancellationToken cancellationToken);
}
