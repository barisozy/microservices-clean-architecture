# ADR 0011: Centralized Event Contracts Package

## Status
Accepted

## Context
In an event-driven architecture, services communicate by publishing and consuming integration events (e.g., `OrderCreated`, `PaymentFailed`). If each microservice defines its own integration event classes locally, producer and consumer logic becomes fragile. This leads to duplicate definitions, namespace mismatches (which break MassTransit deserialization), and contract drift over time.

## Decision
We will extract all integration event definitions (implemented as C# Records) into a standalone, shared class library named `ECommerce.Contracts`. All microservices will reference this single source of truth for their event contracts. Contracts will be explicitly versioned using namespaces (e.g., `ECommerce.Contracts.v1`).

## Consequences
**Positive:**
- Strict contract enforcement: Producers and consumers are guaranteed to use identical event schemas.
- Single source of truth for all cross-service communication payloads.
- Eliminates duplicate code and deserialization errors caused by misaligned namespaces.
- Clear separation between internal Domain Events and external Integration Events.

**Negative:**
- Introduces a shared structural dependency across otherwise independent microservices.
- Requires careful versioning (e.g., creating `v2` namespaces) and backwards compatibility management to prevent breaking changes across the distributed system.
