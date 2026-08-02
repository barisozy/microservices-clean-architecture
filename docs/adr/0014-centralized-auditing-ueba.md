# ADR 0014: Compliance Audit Service and Cross-Cutting Entity Auditing

## Status

Accepted

## Context

The platform needs two distinct audit concerns:

1. Ordinary entities need consistent `CreatedAt`, `CreatedBy`, `LastModifiedAt` and `LastModifiedBy` values.
2. Compliance-relevant security and administration events need an immutable, independently queryable trail.

The former must not turn every domain write into a remote call. The latter must be append-only and detectable if tampered with.

## Decision

`ECommerce.Auditing` remains a small shared building block. Its `AuditableEntityInterceptor` populates audit fields during EF Core saves. It is not a messaging producer and is not the Audit microservice.

`Audit.Api` is the dedicated compliance microservice. It consumes `PermissionDenied`, `CouponWritten` and `UserRegistered` integration events through MassTransit inbox deduplication. It appends `AuditEntries` in its own `audit` schema, with each row linked by SHA-256 to the previous row hash. PostgreSQL permissions revoke `UPDATE` and `DELETE` from the application role; the DbContext also rejects modified or deleted audit entries.

The read-only REST query endpoint requires IAM `ADMIN` permission. The `AuditService.Verify` gRPC method recalculates a requested hash-chain range and reports the first broken entry.

## Consequences

- Business write paths stay independent of the compliance service.
- Compliance records are append-only, idempotent and independently verifiable.
- `ECommerce.Auditing` and `Audit.Api` have intentionally separate responsibilities and names.
