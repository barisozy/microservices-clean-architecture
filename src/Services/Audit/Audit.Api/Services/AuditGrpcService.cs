using Audit.Application.Common.Interfaces;
using ECommerce.Contracts.Protos;
using Grpc.Core;

namespace Audit.Api.Services;

public sealed class AuditGrpcService(IAuditEntryStore store) : AuditService.AuditServiceBase
{
    public override async Task<VerifyResponse> Verify(VerifyRequest request, ServerCallContext context)
    {
        var result = await store.VerifyAsync(
            request.FromId > 0 ? request.FromId : null,
            request.ToId > 0 ? request.ToId : null,
            context.CancellationToken);
        return new VerifyResponse
        {
            Valid = result.Valid,
            BrokenAtId = result.BrokenAtId ?? 0,
            ErrorMessage = result.ErrorMessage ?? string.Empty
        };
    }
}
