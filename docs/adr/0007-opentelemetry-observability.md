# ADR 0007: OpenTelemetry for Distributed Tracing and Metrics

## Status
Accepted

## Context
With multiple microservices communicating synchronously (gRPC) and asynchronously (RabbitMQ), diagnosing performance bottlenecks and errors in production is challenging. We need a standardized way to trace requests as they travel across service boundaries.

## Decision
We will adopt **OpenTelemetry** as the standard for observability (Tracing, Metrics, and Logging) across all .NET 10 microservices. We will inject TraceContext through gRPC interceptors and MassTransit message headers.

## Consequences
**Positive:**
- Vendor-agnostic standard allows switching observability backends (e.g., Jaeger, Zipkin, Prometheus, Application Insights) without changing application code.
- Provides end-to-end visibility of a business process spanning multiple services.
- .NET 10 has excellent native support for OpenTelemetry.

**Negative:**
- Small performance overhead due to telemetry collection and exportation.
- Requires an OpenTelemetry Collector or compatible backend infrastructure.
