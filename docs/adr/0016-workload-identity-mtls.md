# ADR 0016: Workload Identity and mTLS for Production Services

**Date:** 2026-08-15
**Status:** Accepted
**Context:** Security / Infrastructure

## Context
Currently, our microservices rely on User JWT propagation to assert identity and authorize requests across service-to-service communication. While this is acceptable for user-initiated actions, it presents several security and operational challenges in a true production environment:
1. **Service Authentication:** We cannot easily cryptographically verify the identity of the calling service itself (as opposed to the end-user).
2. **Network Security:** Traffic between microservices inside the cluster might be susceptible to sniffing or man-in-the-middle attacks if not properly encrypted at the transport layer.
3. **Background Tasks:** Services performing background operations (e.g., cron jobs, async event consumers) do not have a User JWT context to propagate. 

We need a robust, scalable, and zero-trust approach to service-to-service authentication and encryption in production.

## Decision
We will adopt **Workload Identity** and **Mutual TLS (mTLS)** as the standard for all service-to-service communication in production.

1. **mTLS via Service Mesh / Infrastructure:** 
   - We will utilize an infrastructure-level mechanism (e.g., Istio, Linkerd, or cloud-provider native service mesh) to enforce mTLS between all pods/containers.
   - This ensures that all communication is encrypted in transit and the identity of the participating services is verified via short-lived X.509 certificates.
   - The application layer (C# code) will be agnostic to this encryption; it will continue communicating over HTTP/2, while the proxy/mesh handles TLS termination.

2. **Workload Identity (SPIFFE / Cloud Native):**
   - Each service will be assigned a distinct identity (e.g., Kubernetes Service Account mapped to an AWS IAM Role, Azure Managed Identity, or SPIFFE ID).
   - For background or service-to-service operations not bound to a specific user context, services will request tokens using their Workload Identity rather than using a static shared secret or generic system user account.

3. **Hybrid Authorization (User + Service):**
   - Where a user context exists, the User JWT will still be propagated via the `Authorization: Bearer` header.
   - The mTLS identity will establish the caller's *service* identity.
   - Services like IAM and Audit can use the service identity to enforce zero-trust policies (e.g., "Only the `Order` service is allowed to call `Inventory.ReserveStock`").

## Consequences
**Positive:**
- **Zero-Trust Security:** Strong cryptographic guarantee of the caller's identity.
- **Compliance:** Meets stringent compliance requirements for encryption of data in transit within the internal network.
- **Operational Simplicity:** Removes the need for static credentials, API keys, or long-lived secrets for inter-service communication.

**Negative/Risks:**
- **Complexity:** Requires setting up and managing a Service Mesh or Workload Identity provider in the production Kubernetes/Cloud environment.
- **Debugging:** Local development and debugging become slightly more complex if trying to replicate production identity fully (though `IsProduction()` toggles will allow bypass in Dev).
- **Performance Overhead:** Minor latency overhead due to mTLS handshakes and proxy sidecars. 
