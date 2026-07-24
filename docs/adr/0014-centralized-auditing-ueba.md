# 14. Centralized Auditing & UEBA Telemetry

Date: 2026-07-23

## Status

Accepted

## Context

Enterprise-grade microservices require a unified trail of data modification events (Creates, Updates, Deletes) to ensure compliance, non-repudiation, and security auditing. Beyond just knowing "what" changed in the database, security teams require **User and Entity Behavior Analytics (UEBA)** telemetry: the user's ID, IP address, device footprint (User Agent), and the precise API endpoint that triggered the action.

Implementing this per microservice leads to:
1. Fragmented data that is difficult to query.
2. Boilerplate code replicated across services.
3. Tight coupling to specific storage technologies within domains that shouldn't know about auditing.

We needed a centralized, resilient mechanism to intercept EF Core data changes, extract UEBA telemetry from the HTTP context, and funnel this information reliably to a specialized Auditing Service.

## Decision

We will implement a **Centralized Auditing Service** with distributed, asynchronous event collection:

1. **`ECommerce.Auditing` (Shared Package):**
   - We extract auditing interception into a reusable NuGet-like shared project.
   - We implement `AuditableEntityInterceptor`, an EF Core `ISaveChangesInterceptor`. This interceptor automatically scans the `ChangeTracker` for entities implementing `IAuditableEntity`.
   - Before changes are committed, it extracts the HTTP Context metadata (`UserIp`, `UserAgent`, `Endpoint`, `UserId`) using `IHttpContextAccessor`.
   - It serializes the entity's Old Values, New Values, and Primary Keys into JSON.

2. **Asynchronous Publishing (RabbitMQ):**
   - The interceptor constructs an `AuditLogCreatedEvent` and publishes it to the event bus (RabbitMQ via MassTransit) rather than writing synchronously to a database.
   - This prevents audit logging from adding synchronous latency or creating a single point of failure (if the audit DB is down, transactions shouldn't fail).

3. **`Auditing.Api` (Centralized Microservice):**
   - A dedicated `Auditing.Api` microservice consumes `AuditLogCreatedEvent` messages from RabbitMQ.
   - It writes these logs to an isolated `AuditingDb` (PostgreSQL) optimized for temporal and JSONB querying.
   - It provides specialized endpoints (e.g., `/api/v1/audit-logs`) to query and filter changes by user, entity, or UEBA attributes.

## Consequences

- **Pros:**
  - **Zero-Touch Auditing:** Developers just add `IAuditableEntity` to a domain model. The interception handles the rest transparently.
  - **High Performance:** Asynchronous publishing ensures the primary transaction is not delayed by audit storage.
  - **Security Observability:** Full UEBA telemetry is available for all state-changing operations across the entire platform.
- **Cons:**
  - Increases RabbitMQ message volume.
  - If a service commits a transaction but crashes before the outbox/event is dispatched to RabbitMQ, the audit log might theoretically be delayed (handled by MassTransit Outbox guaranteeing at-least-once delivery).
