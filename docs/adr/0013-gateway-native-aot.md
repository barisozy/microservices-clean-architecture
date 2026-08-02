# ADR 0013: Native AOT Is Deferred

## Status

Accepted

## Context

The Gateway uses YARP, JWT authentication, configuration-driven route discovery and observability libraries. The backend services also depend on EF Core, whose Native AOT support is not complete for this platform.

## Decision

The platform targets the standard .NET 10 runtime. Native AOT is deferred for the Gateway and all backend services. No project sets `PublishAot`, and the standard multi-stage container images use the .NET runtime image.

## Consequences

- Runtime behavior remains compatible with YARP, EF Core, reflection-based framework features and diagnostics.
- Cold-start and image-size improvements from AOT are postponed.
- A future AOT decision requires a dedicated compatibility, trimming and operational benchmark.
