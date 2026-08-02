using Audit.Application.Common.Interfaces;
using MediatR;
using FluentValidation;

namespace Audit.Application.AuditEntries;

public sealed record GetAuditEntriesQuery(
    string? Actor,
    string? Action,
    DateTimeOffset? From,
    DateTimeOffset? To,
    long? Cursor,
    int Limit = 50) : IRequest<AuditEntryPage>;
public sealed record VerifyAuditChainQuery(long? FromId, long? ToId) : IRequest<AuditVerificationResult>;

public sealed class GetAuditEntriesQueryValidator : AbstractValidator<GetAuditEntriesQuery>
{
    public GetAuditEntriesQueryValidator()
    {
        RuleFor(request => request.Actor).MaximumLength(200);
        RuleFor(request => request.Action).MaximumLength(200);
        RuleFor(request => request.Cursor).GreaterThanOrEqualTo(0).When(request => request.Cursor.HasValue);
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
        RuleFor(request => request).Must(request => !request.From.HasValue || !request.To.HasValue || request.From <= request.To)
            .WithMessage("The 'from' timestamp must not be later than 'to'.");
    }
}

public sealed class VerifyAuditChainQueryValidator : AbstractValidator<VerifyAuditChainQuery>
{
    public VerifyAuditChainQueryValidator()
    {
        RuleFor(request => request.FromId).GreaterThan(0).When(request => request.FromId.HasValue);
        RuleFor(request => request.ToId).GreaterThan(0).When(request => request.ToId.HasValue);
        RuleFor(request => request).Must(request => !request.FromId.HasValue || !request.ToId.HasValue || request.FromId <= request.ToId)
            .WithMessage("FromId must not be greater than ToId.");
    }
}

public sealed class GetAuditEntriesQueryHandler(IAuditEntryStore store)
    : IRequestHandler<GetAuditEntriesQuery, AuditEntryPage>
{
    public Task<AuditEntryPage> Handle(GetAuditEntriesQuery request, CancellationToken cancellationToken) =>
        store.QueryAsync(
            request.Actor,
            request.Action,
            request.From,
            request.To,
            request.Cursor,
            Math.Clamp(request.Limit, 1, 100),
            cancellationToken);
}

public sealed class VerifyAuditChainQueryHandler(IAuditEntryStore store)
    : IRequestHandler<VerifyAuditChainQuery, AuditVerificationResult>
{
    public Task<AuditVerificationResult> Handle(VerifyAuditChainQuery request, CancellationToken cancellationToken) =>
        store.VerifyAsync(request.FromId, request.ToId, cancellationToken);
}
