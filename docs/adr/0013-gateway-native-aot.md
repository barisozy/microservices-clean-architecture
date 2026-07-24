# ADR 0013: API Gateway Native AOT Compilation

## Status
Accepted

## Context
The YARP API Gateway (`ECommerce.Gateway`) acts as the ingress point for all external traffic. In high-throughput, cloud-native environments (like Kubernetes), the gateway must scale rapidly, consume minimal memory, and have virtually zero cold-start latency. Traditional .NET JIT (Just-In-Time) compilation introduces a noticeable startup delay and higher base memory footprint.

## Decision
We enabled **.NET Native AOT (Ahead-of-Time)** compilation for the `ECommerce.Gateway` project.
- The project is published with `<PublishAot>true</PublishAot>`.
- Reflection-heavy libraries were replaced or configured with source generators (e.g., System.Text.Json source generators) where necessary to ensure trimming and AOT compatibility.

## Consequences
### Positive
- **Instant Cold Starts:** Startup time dropped from ~1.5s to sub-50ms, allowing instantaneous horizontal scaling.
- **Lower Memory:** Base memory footprint dropped significantly, allowing higher density of gateway replicas per node.
- **Smaller Image Size:** The Docker image requires only the alpine runtime dependencies, not the full .NET runtime.

### Negative
- **Build Time:** The CI/CD pipeline takes slightly longer for the Gateway because Native AOT requires heavy native compilation (ILC).
- **Constraints:** Dynamic code generation and certain reflection-based techniques are no longer permitted in the Gateway project.
