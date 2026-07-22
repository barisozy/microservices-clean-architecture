# ADR 0005: Transactional Outbox Pattern for Distributed Transactions

## Status
Accepted

## Context
In an event-driven architecture, services must update their local database and publish events to the message broker atomically. If a service updates the database but crashes before publishing the event, the system falls into an inconsistent state (Dual Write problem).

## Decision
We will implement the **Transactional Outbox Pattern** using the **MassTransit Entity Framework Core Outbox** integration. Instead of directly publishing messages to RabbitMQ, messages are written to an `OutboxMessage` table within the same database transaction as the business entity changes. A background worker then reads from this table and reliably dispatches the messages to RabbitMQ.

## Consequences
**Positive:**
- Guarantees at-least-once delivery of events.
- Prevents data inconsistencies between local state and message broker.
- MassTransit fully manages the Outbox tables and background polling/delivery.

**Negative:**
- Adds extra tables to every service's database.
- Introduces slight latency between transaction commit and event dispatch.
