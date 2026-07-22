# ADR 0003: gRPC for Synchronous Inter-Service Communication

## Status
Accepted

## Context
While the architecture favors asynchronous event-driven communication for state changes, there are scenarios where a service needs immediate, synchronous data from another service (e.g., Ordering needing to query Inventory status synchronously). Standard HTTP/REST JSON APIs can have higher serialization overhead and lack strict typing.

## Decision
We will use **gRPC** for synchronous service-to-service communication within the backend.

## Consequences
**Positive:**
- High performance with Protobuf binary serialization and HTTP/2.
- Strongly typed contracts (.proto files) ensure both sides agree on the data structure.
- Built-in support for cancellation tokens and deadlines.

**Negative:**
- Harder to debug using standard HTTP tools (requires gRPC specific clients like Postman or grpcurl).
- Requires sharing .proto definitions across service boundaries.
