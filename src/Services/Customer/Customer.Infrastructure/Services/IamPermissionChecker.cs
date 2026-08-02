using Customer.Application.Common.Interfaces;
using ECommerce.Contracts.Protos;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Customer.Infrastructure.Services;

public sealed class IamPermissionChecker(
    IamService.IamServiceClient client,
    ILogger<IamPermissionChecker> logger) : IIamPermissionChecker
{
    public async Task<bool> IsAllowedAsync(
        string subject,
        string permission,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.CheckPermissionAsync(
                new CheckPermissionRequest { Subject = subject, Permission = permission },
                cancellationToken: cancellationToken);
            return response.Allowed;
        }
        catch (RpcException exception)
        {
            logger.LogWarning(exception, "IAM permission check failed for {Permission}", permission);
            return false;
        }
    }
}
