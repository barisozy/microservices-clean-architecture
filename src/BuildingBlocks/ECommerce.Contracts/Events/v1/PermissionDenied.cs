namespace ECommerce.Contracts.Events.v1;

public record PermissionDenied(
    string ActorSubject,
    string Permission,
    string Resource,
    DateTimeOffset OccurredAt
);
