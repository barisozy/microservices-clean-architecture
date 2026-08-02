# ADR 0009: Service-Level CQRS with Read/Write Separation

## Status
Accepted

## Context
As the microservices grow, the read and write characteristics of the system diverge. Write operations require strict transactional integrity, domain validations, and ACID compliance, which PostgreSQL provides. However, read operations (like querying order status or inventory availability) require extremely low latency and high throughput. Storing and querying from the same relational schema creates unnecessary locking and database contention.

## Decision
We will enforce CQRS (Command Query Responsibility Segregation) at the service level, backed by physical read/write separation:
- **Write Model (PostgreSQL)**: All commands (e.g., creating orders, reserving stock) are executed against the domain entities and persisted to PostgreSQL using EF Core.
- **Read Model**: Each service uses a dedicated read path rather than exposing its write-side DbContext to API endpoints. Latency-sensitive projections such as Order and Inventory availability are maintained in Valkey (Redis-compatible cache); Search owns a PostgreSQL `tsvector` read model, while Catalog, Customer, Promotion, IAM and Audit use service-local query repositories with output/cache policies where appropriate.

## Consequences
**Positive:**
- Blazing fast read operations for Valkey-backed projections while preserving service-specific search and compliance query capabilities.
- Clean separation of concerns in the Application layer (MediatR Commands vs. Queries).
- Independent scaling of read and write storage layers.

**Negative:**
- Increased infrastructure complexity (requires both PostgreSQL and Valkey per service domain).
- Codebase overhead: requires explicit synchronization logic between the Write and Read models.
