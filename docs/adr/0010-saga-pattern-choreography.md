# ADR 0010: Choreography-Based Saga Pattern for Distributed Transactions

## Status
Accepted

## Context
A complete e-commerce transaction spans multiple microservices: an order is created (Ordering), stock is reserved (Inventory), payment is processed (Payments), and the items are shipped (Fulfillment). Since microservices hold independent databases to adhere to true microservice principles, we cannot use a traditional 2PC (Two-Phase Commit) transaction. If any step fails (e.g., payment is rejected), all previously successful steps must be rolled back to maintain eventual consistency.

## Decision
We will implement the **Choreography-based Saga Pattern** using MassTransit and RabbitMQ. 
Each service reacts to domain events, executes its local database transaction (using the Outbox pattern), and publishes the next event in the flow. If a failure occurs, the failing service publishes a compensation event (e.g., `PaymentFailed`). The preceding services listen to this compensation event and execute their compensating actions (e.g., `StockReleased`, `OrderCancelled`).

## Consequences
**Positive:**
- No single point of failure (eliminates the need for a central orchestrator bottleneck).
- High decoupling; services only know about the events they consume and produce, maintaining domain boundaries.
- Highly resilient and scalable for distributed business workflows.

**Negative:**
- Harder to track the overall status of the transaction from a single point (mitigated by using OpenTelemetry distributed tracing).
- More complex to design, test, and debug compensation logic across multiple isolated services.
