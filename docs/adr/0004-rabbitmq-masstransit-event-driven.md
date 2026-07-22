# ADR 0004: Event-Driven Architecture with RabbitMQ and MassTransit

## Status
Accepted

## Context
To ensure loose coupling between microservices and to handle distributed business processes (e.g., Order Creation leading to Payment and Inventory Reservation), services need a way to communicate asynchronously without waiting for downstream services.

## Decision
We will adopt an **Event-Driven Architecture** utilizing **RabbitMQ** as the message broker. We will use **MassTransit** as the message bus abstraction layer in .NET 10.

## Consequences
**Positive:**
- Loose coupling: Services do not need to know about each other's availability at runtime.
- MassTransit provides a powerful abstraction over RabbitMQ, reducing boilerplate code.
- Out-of-the-box support for modern patterns like Sagas, Outbox, and Retry mechanisms.

**Negative:**
- Eventual consistency complicates UI/UX and debugging.
- Managing message broker infrastructure requires specialized knowledge.
