# ADR 0015: Checkout Saga State Machine and Inventory Leases

## Status
Accepted

## Decision
Checkout starts by durably persisting a pending Order and `CheckoutStarted` in the Order transactional outbox. A MassTransit 8.5.4 EF-backed state machine orchestrates asynchronous `ReserveInventory`, `PaymentRequested`, and `CommitInventoryReservation` commands. Inventory owns reservation state and uses `Pending`, `Committed`, `Released`, and `Expired` transitions. Inventory commit occurs only after successful payment completion.

Pending reservations have a server-side two-minute lease. A PostgreSQL `FOR UPDATE SKIP LOCKED` reaper releases expired stock as a last-resort safety net. Saga timeout is the active recovery path; the lease remains independent recovery when the saga or broker is unavailable. Payment completion followed by inventory-commit failure enters refund compensation.

No distributed transaction or cross-service database access is used. Inventory and Order each use their own EF transactional outbox/inbox and idempotent state transitions.

## Consequences

- `POST /orders` returns `202 Accepted` with `PendingInventory`; clients poll the order resource.
- Checkout is observable through one deterministic correlation identity: `OrderId == Saga CorrelationId`.
- Inventory expiry is exceptional and separately measurable from explicit compensation.
- The old synchronous Order-to-Inventory gRPC checkout path is no longer used; gRPC remains available for other supported service contracts.
