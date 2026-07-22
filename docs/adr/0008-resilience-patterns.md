# ADR 0008: Resilience Patterns for Network Fault Tolerance

## Status
Accepted

## Context
Microservices rely heavily on network communication (gRPC, HTTP, RabbitMQ). Networks are inherently unreliable, and temporary outages or service degradation can cascade and bring down the entire system.

## Decision
We will systematically apply **Resilience Patterns** across all inter-service communications using standard .NET 10 libraries:

1. **Synchronous Communications (gRPC/HTTP):** We use `Microsoft.Extensions.Http.Resilience` (Polly v8+) via `AddStandardResilienceHandler()`. This provides:
   - **Retry:** Automatically retry failed transient requests (MaxRetryAttempts = 3, Exponential Backoff).
   - **Circuit Breaker:** Stop sending requests to a completely failing service to prevent system overload (FailureRatio = 0.5).
   - **Timeouts & Deadlines:** Enforce strict SLAs to avoid unbounded waiting (Attempt Timeout and Global Timeout).

2. **Asynchronous Communications (MassTransit):**
   - **Retry Policy:** Events are retried automatically upon processing failure (Retry x3 with interval).
   - **Dead Letter Queue (DLQ) & Poison Message Handling:** Messages that repeatedly fail are moved to error/fault queues to avoid blocking the queue processing.

## Consequences
**Positive:**
- System handles temporary network glitches gracefully without user impact.
- Cascading failures are prevented via Circuit Breakers.
- Poison messages do not block healthy message processing.

**Negative:**
- Retries can cause idempotency issues; receivers must be idempotent.
- Finding the perfect timeout and retry values requires monitoring and tuning.
