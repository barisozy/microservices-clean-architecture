# ADR 0009: Service-Level CQRS with Read/Write Separation

## Status
Accepted

## Context
As the microservices grow, the read and write characteristics of the system diverge. Write operations require strict transactional integrity, domain validations, and ACID compliance, which PostgreSQL provides. However, read operations (like querying order status or inventory availability) require extremely low latency and high throughput. Storing and querying from the same relational schema creates unnecessary locking and database contention.

## Decision
We will enforce CQRS (Command Query Responsibility Segregation) at the service level, backed by physical read/write separation:
- **Write Model (PostgreSQL)**: All commands (e.g., creating orders, reserving stock) are executed against the domain entities and persisted to PostgreSQL using EF Core.
- **Read Model (Valkey)**: Upon a successful write, a pre-computed read model is updated and persisted to Valkey (Redis-compatible cache). All queries fetch data directly from Valkey via specialized ReadRepositories, achieving O(1) read performance.

## Consequences
**Positive:**
- Blazing fast read operations (sub-millisecond) offloading pressure from PostgreSQL.
- Clean separation of concerns in the Application layer (MediatR Commands vs. Queries).
- Independent scaling of read and write storage layers.

**Negative:**
- Increased infrastructure complexity (requires both PostgreSQL and Valkey per service domain).
- Codebase overhead: requires explicit synchronization logic between the Write and Read models.
