# ADR 0007: OpenTelemetry for Distributed Tracing and Metrics

## Status
Accepted

## Context
With multiple microservices communicating synchronously (gRPC) and asynchronously (RabbitMQ), diagnosing performance bottlenecks and errors in production is challenging. We need a standardized way to trace requests as they travel across service boundaries.

## Decision
We will adopt **OpenTelemetry** as the standard for traces and metrics across all .NET 10 microservices. gRPC interceptors forward W3C trace context; MassTransit propagates it over RabbitMQ. Aspire Dashboard is the MVP OTLP backend and has ephemeral retention.

## Consequences
**Positive:**
- Vendor-agnostic instrumentation allows a future exporter change without changing application code.
- Provides end-to-end visibility of a business process spanning multiple services.
- .NET 10 has excellent native support for OpenTelemetry.

**Negative:**
- Small performance overhead due to telemetry collection and exportation.
- Aspire Dashboard is not long-term telemetry storage; a durable backend is required when retention becomes a requirement.
