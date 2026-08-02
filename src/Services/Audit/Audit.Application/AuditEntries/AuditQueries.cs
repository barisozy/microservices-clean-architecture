using Audit.Application.Common.Interfaces;
using MediatR;

namespace Audit.Application.AuditEntries;

public sealed record GetAuditEntriesQuery(
    string? Actor,
    string? Action,
    DateTimeOffset? From,
    DateTimeOffset? To,
    long? Cursor,
    int Limit = 50) : IRequest<AuditEntryPage>;

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
