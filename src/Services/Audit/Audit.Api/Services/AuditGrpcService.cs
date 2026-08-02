using Audit.Application.AuditEntries;
using ECommerce.Contracts.Protos;
using FluentValidation;
using Grpc.Core;
using MediatR;

namespace Audit.Api.Services;

public sealed class AuditGrpcService(
    ISender sender,
    IValidator<VerifyAuditChainQuery> validator) : AuditService.AuditServiceBase
{
    public override async Task<VerifyResponse> Verify(VerifyRequest request, ServerCallContext context)
    {
        var query = new VerifyAuditChainQuery(
            request.FromId > 0 ? request.FromId : null,
            request.ToId > 0 ? request.ToId : null);
        var validation = await validator.ValidateAsync(query, context.CancellationToken);
        if (!validation.IsValid)
        {
            return new VerifyResponse
            {
                Valid = false,
                ErrorMessage = string.Join("; ", validation.Errors.Select(error => error.ErrorMessage))
            };
        }
        var result = await sender.Send(query, context.CancellationToken);
        return new VerifyResponse
        {
            Valid = result.Valid,
            BrokenAtId = result.BrokenAtId ?? 0,
            ErrorMessage = result.ErrorMessage ?? string.Empty
        };
    }
}
