namespace ECommerce.Contracts.Events.v1;

public record CouponWritten(
    string ActorSubject,
    string Code,
    string Action,
    DateTimeOffset OccurredAt
);
