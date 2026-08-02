namespace Customer.Application.Common.Interfaces;

public interface IIamPermissionChecker
{
    Task<bool> IsAllowedAsync(string subject, string permission, CancellationToken cancellationToken = default);
}
