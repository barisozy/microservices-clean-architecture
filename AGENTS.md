# ECommerce Microservices Platform — Architectural Specification

> **Audience:** AI coding agents, technical contributors, senior engineers.
> **Authority:** Principal Software Architect — every decision here is a binding constraint.
> **Reference plan:** `plan/microservices-sprint-plan-e-commerce - E-Commerce Platform.csv`
> **Last updated:** 2026-07-31

---

## 1. System Identity

**Production-grade true microservices platform** — independent data ownership, independent deployability, per-service CI/CD. Not a modular monolith with service-shaped folders.

| Property | Value |
|----------|-------|
| Language | C# 14 |
| Runtime | .NET 10.0.9 LTS (supported → 2028-11-14) |
| SDK pin | `global.json` → `10.0.9`, rollForward: `latestFeature` |
| Aspire SDK | `Aspire.AppHost.Sdk` 9.1.0 |
| Architecture | Onion (Domain → Application → Infrastructure → Api), enforced via `.csproj` reference graph |
| Deployment | Monorepo + independent per-service Docker images + path-filtered GitHub Actions |
| Service count | 12 (Gateway + 11 backends) — hard cap |

---

## 2. Non-Negotiable Invariants

Violations produce **build errors**, not review comments.

### 2.1 Onion Dependency Direction

```
┌─────────────────────────────┐
│         Service.Api         │  ASP.NET Core, Minimal APIs, Scalar/OpenAPI
│  (outermost)                │
└─────────────┬───────────────┘
┌─────────────▼───────────────┐
│    Service.Infrastructure   │  EF Core, MassTransit, Valkey, gRPC clients
└─────────────┬───────────────┘
┌─────────────▼───────────────┐
│     Service.Application     │  MediatR CQRS, FluentValidation, Consumers
└─────────────┬───────────────┘
┌─────────────▼───────────────┐
│       Service.Domain        │  Entities, Domain Events, Enums, Exceptions
│   ZERO external deps        │  No EF Core. No MassTransit. No ASP.NET Core.
└─────────────────────────────┘
```

- **Domain.csproj:** zero `PackageReference` to EF Core / Grpc.* / ASP.NET Core / MassTransit. Zero `ProjectReference` to Infrastructure or Api.
- **Application.csproj:** zero infrastructure packages (Npgsql, StackExchange.Redis, MassTransit.RabbitMQ, Grpc.AspNetCore). May reference `MassTransit` core for `IPublishEndpoint`.

### 2.2 Data Isolation

Each service owns exactly one Postgres schema. No shared tables, no cross-schema JOINs, no shared `DbContext`. Legal cross-service data access:
1. Synchronous gRPC call
2. Async integration event (MassTransit / RabbitMQ)
3. Local read-model built from consumed events

### 2.3 No Shared Compiled Code Between Services

Services never `ProjectReference` each other. Shared code is limited to:
- `ECommerce.Contracts` — integration event records + `.proto` files
- `ECommerce.ServiceDefaults` — OTel, health checks, resilience, gRPC interceptors
- `ECommerce.Auditing` — cross-cutting audit `SaveChangesInterceptor` (`BaseAuditableEntity` tracking: `CreatedAt`/`By`, `LastModifiedAt`/`By`), distinct from `Audit.Api` compliance service

### 2.4 Version Locks

| Constraint | Reason |
|-----------|--------|
| MassTransit pinned at **8.5.4** (MIT) | v9 commercial license ($400–1,200/mo), Q1 2026. Do NOT upgrade. |
| **Valkey 9.1** (BSD-3-Clause), never Redis | Wire-compatible RESP. No AGPL/SSPL ambiguity. ~20% lower memory. |

---

## 3. Tech Stack — Complete Dependency Catalog

### 3.1 Core Runtime & Framework

| Component | Version (Pinned) | Purpose |
|-----------|-----------------|---------|
| .NET SDK / C# 14 | 10.0.9 LTS | Runtime. Native AOT deferred — EF Core AOT partial in .NET 10. |
| ASP.NET Core | 10.0.10 (in-box) | Minimal APIs, Kestrel |
| NuGet Central Package Mgmt | `Directory.Packages.props` | Repo-root platform-version pins |

### 3.2 Gateway & Reverse Proxy

| Component | Version | Purpose |
|-----------|---------|---------|
| YARP (`Yarp.ReverseProxy`) | 2.3.0 | Config/code-first routing, gRPC/HTTP2/HTTP3-aware |
| `RedisRateLimiting` | 1.2.1 | Valkey-backed sliding window rate limiter |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | 10.0.10 | Distributed cache backing |

### 3.3 Persistence & ORM

| Component | Version | Purpose |
|-----------|---------|---------|
| Entity Framework Core | 10.0.10 | Code-first, migrations |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | `SetPostgresVersion(18, 0)` → UUID v7 native |
| PostgreSQL | 18.4 | One logical schema per service, single instance MVP |

### 3.4 Messaging

| Component | Version | Purpose |
|-----------|---------|---------|
| RabbitMQ | 4.3.1-management | Single node MVP. UI at `:15672` |
| `MassTransit` | 8.5.4 | Core + `IPublishEndpoint` |
| `MassTransit.RabbitMQ` | 8.5.4 | RabbitMQ transport |
| `MassTransit.EntityFrameworkCore` | 8.5.4 | EF Core Transactional Outbox |
| `RabbitMQ.Client` | 7.1.2 | Underlying AMQP client |

### 3.5 Cache, Lock & Rate Limiting

| Component | Version | Purpose |
|-----------|---------|---------|
| Valkey | 9.1 (BSD-3-Clause) | Cache/lock/rate-limit. Disposable state — no AOF. |
| `StackExchange.Redis` | 3.0.17 | RESP client for all Valkey access |
| `Aspire.StackExchange.Redis` | 9.0.0 | DI + auto health-check + OTel |
| `DistributedLock.Redis` | 1.1.1 | Single-instance mutex. No Redlock claim. |

### 3.6 gRPC / Inter-Service RPC

| Component | Version | Purpose |
|-----------|---------|---------|
| `Grpc.AspNetCore` | 2.80.0 | gRPC server hosting |
| `Grpc.Net.Client` | 2.80.0 | gRPC client, HttpClientFactory-integrated |
| `Google.Protobuf` | 3.35.1 | Proto serialization |
| `Grpc.Tools` | 2.82.0 | `.proto` → C# codegen |

### 3.7 CQRS & Validation

| Component | Version | Purpose |
|-----------|---------|---------|
| MediatR | 12.0.1 | CQRS command/query dispatch |
| FluentValidation | 12.1.1 | Request DTO validation (Apache 2.0) |

### 3.8 Security

| Component | Version | Purpose |
|-----------|---------|---------|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.10 (in-box) | JWT on Gateway AND every backend — defense in depth |
| Keycloak | 26.6.4 | IdP. Realm `ecommerce`, roles CUSTOMER/ADMIN |
| `Microsoft.Extensions.Http.Resilience` | 10.8.0 | Polly v8 resilience pipelines |

### 3.9 Observability

| Component | Version | Purpose |
|-----------|---------|---------|
| `OpenTelemetry.Extensions.Hosting` | 1.17.0 | OTel host integration |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.17.0 | OTLP push to Aspire Dashboard |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.17.0 | HTTP span auto-instrumentation |
| `OpenTelemetry.Instrumentation.Http` | 1.17.0 | HttpClient span instrumentation |
| `OpenTelemetry.Instrumentation.GrpcNetClient` | 1.12.0-beta.1 | gRPC span instrumentation |
| `OpenTelemetry.Instrumentation.Runtime` | 1.17.0 | Runtime metrics |
| `Serilog.AspNetCore` | 10.0.0 | Structured JSON logging, TraceId/SpanId bridge |
| `Scalar.AspNetCore` | 2.16.16 | OpenAPI UI |

### 3.10 Aspire Orchestration

| Component | Version | Purpose |
|-----------|---------|---------|
| `Aspire.Hosting.AppHost` | 13.4.6 | Full app model in C# |
| `Aspire.Hosting.PostgreSQL` | 13.4.6 | Typed Postgres resource |
| `Aspire.Hosting.RabbitMQ` | 13.4.6 | Typed RabbitMQ resource |
| `Aspire.Hosting.Redis` | 13.4.6 | Typed Valkey resource |
| `Aspire.Hosting.Testing` | 13.4.6 | Integration test host |
| Aspire Dashboard | Auto (13.4.x) | Ephemeral OTel backend — no long-term retention |

### 3.11 Testing

| Component | Version | Purpose |
|-----------|---------|---------|
| xUnit (xunit.v3) | 3.2.2 | Test framework, MTP v2 |
| Shouldly | 4.3.0 | MIT assertions (over FluentAssertions v8+ paid) |
| Moq | 4.20.72 | Mocking |
| `Moq.EntityFrameworkCore` | 10.0.0.2 | DbContext mocking |
| Testcontainers | 4.13.0 | + `.PostgreSql`, `.RabbitMq` |
| PactNet | 4.5.0 | Consumer-driven contract tests |
| `Microsoft.NET.Test.Sdk` | 18.8.1 | Test SDK |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.10 | WebApplicationFactory |
| `coverlet.collector` / `.msbuild` | 10.0.1 | Code coverage |

### 3.12 API Conventions (Sprint 7 rollout)

| Component | Version | Purpose |
|-----------|---------|---------|
| `Asp.Versioning.Http` | 8.1.0 | `UrlSegmentApiVersionReader`, `/api/v1/` formalized |
| `ProblemDetails` (in-box) | 10.0.10 | RFC 9457 `application/problem+json` on all 4xx/5xx |

---

## 4. Repository Layout

```
ECommerce/
├── src/
│   ├── BuildingBlocks/
│   │   ├── ECommerce.Contracts/           # Integration events (C# records) + .proto files
│   │   │   ├── Events/v1/                 # OrderCreated, PaymentCompleted, PaymentFailed,
│   │   │   │                              # OrderCancelled, OrderShipped, StockReleased,
│   │   │   │                              # StockReserved, UserRegistered, ProductUpserted,
│   │   │   │                              # PermissionDenied, CouponWritten
│   │   │   └── Protos/                    # inventory.proto, iam.proto, catalog.proto, promotion.proto, audit.proto
│   │   └── ECommerce.ServiceDefaults/     # AddBasicServiceDefaults(), OTel, gRPC interceptors, Polly v8
│   │
│   ├── Gateways/
│   │   └── ECommerce.Gateway/             # YARP 2.3.0, JWT, Valkey rate limiting, bulkhead, 12 routes
│   │
│   ├── Orchestration/
│   │   └── ECommerce.AppHost/             # Aspire AppHost: entire system model in C#
│   │
│   └── Services/                          # 11 backends — each follows identical 4-layer Onion
│       ├── Order/                         # Sprint 1–3 — Orders, Basket (Valkey Hash), CQRS
│       ├── Inventory/                     # Sprint 1–3 — Stock reservations, Output Cache
│       ├── Payment/                       # Sprint 1–3 — Mock payment, failure simulation
│       ├── Fulfillment/                   # Sprint 1–3, deepened Sprint 7 (Shipments table)
│       ├── IAM/                           # Sprint 4 — Keycloak Admin API façade, permissions
│       ├── Catalog/                       # Sprint 5 — Products, Categories, Brands, Variants
│       ├── Customer/                      # Sprint 5 — Profiles, addresses, preferences
│       ├── Search/                        # Sprint 6 — Postgres tsvector full-text search
│       ├── Notification/                  # Sprint 6 — Mock email, event-driven only
│       ├── Promotion/                     # Sprint 7 — Coupons, Campaigns, discount calc
│       └── Audit/                         # Sprint 9 — Compliance audit trail (SHA-256 hash chain)
│
├── tests/
│   ├── Order.UnitTests/
│   ├── Inventory.UnitTests/
│   ├── Payment.UnitTests/
│   ├── Fulfillment.UnitTests/
│   ├── IAM.UnitTests/
│   ├── Catalog.UnitTests/
│   ├── Customer.UnitTests/
│   ├── Search.UnitTests/
│   ├── Notification.UnitTests/
│   ├── Promotion.UnitTests/
│   ├── Audit.UnitTests/
│   ├── ECommerce.IntegrationTests/        # Testcontainers E2E
│   └── ECommerce.ContractTests/           # PactNet consumer-driven contracts
│
├── plan/                                  # Sprint CSV — authoritative delivery spec
├── docs/                                  # ADRs
├── infra/                                 # Docker Compose init scripts
├── templates/                             # Project templates
├── .github/workflows/                     # Per-service path-filtered CI/CD
├── Directory.Packages.props               # Central NuGet version catalog
├── Directory.Build.props                  # Shared MSBuild properties
├── global.json                            # SDK 10.0.9 + Aspire.AppHost.Sdk 9.1.0
├── docker-compose.yml                     # Generated by `aspire publish` — never hand-author
└── ECommerce.sln
```

---

## 5. The 12 Services — Canonical Reference

| # | Service | Sprint | Responsibilities | Sync (gRPC) | Async (MassTransit) |
|---|---------|--------|-----------------|-------------|---------------------|
| 1 | **Gateway** (YARP) | 1 | JWT validation, rate limiting, bulkhead, reverse proxy | — | — |
| 2 | **Order.Api** | 1–3 | Orders, Basket (Valkey Hash), CQRS, Outbox | Client → Inventory `ReserveStock`/`ReleaseStock`; Client → Catalog `GetPriceSnapshot`; Client → Promotion `ApplyCoupon` | Pub: `OrderCreated`, `OrderCancelled`. Con: `StockReleased`, `PaymentFailed` |
| 3 | **Inventory.Api** | 1–3 | Stock reservations, availability (Output Cache 5s) | Server: `ReserveStock`, `ReleaseStock` | Pub: `StockReserved`, `StockReleased`. Con: `OrderCreated`, `OrderCancelled`, `PaymentFailed` |
| 4 | **Payment.Api** | 1–3 | Mock payment, failure simulation, compensation | — | Pub: `PaymentCompleted`, `PaymentFailed`. Con: `StockReserved` |
| 5 | **Fulfillment.Api** | 1–3, 7 | Shipments table, `GET /shipments/{orderId}` | — | Pub: `OrderShipped`. Con: `PaymentCompleted` |
| 6 | **IAM.Api** | 4 | Keycloak Admin API façade, invitations, groups, permissions | Server: `CheckPermission` | Pub: `UserRegistered`, `UserProvisioned`. Con: none |
| 7 | **Catalog.Api** | 5 | Products, Categories, Brands, Variants, Images, price snapshot | Server: `GetPriceSnapshot` | Pub: `ProductUpserted`. Con: none |
| 8 | **Customer.Api** | 5 | Profiles, addresses, preferences — decoupled from Keycloak | — | Con: `UserRegistered` |
| 9 | **Search.Api** | 6 | Postgres `tsvector` full-text search read-model | — | Con: `ProductUpserted` |
| 10 | **Notification.Api** | 6 | Mock email — event-driven only, no REST surface | — | Con: `OrderShipped`, `PaymentFailed` |
| 11 | **Promotion.Api** | 7 | Coupons, Campaigns, discount calculation | Server: `ApplyCoupon` | — |
| 12 | **Audit.Api** | 9 | Compliance audit trail, SHA-256 hash chain, DB REVOKE UPDATE/DELETE | Server: `AuditService.Verify` | Con: `PermissionDenied`, `CouponWritten`, `UserRegistered` |

---

## 6. Per-Service Internal Structure

```
Services/{ServiceName}/
├── {Name}.Domain/
│   ├── Common/
│   │   ├── BaseEntity.cs                 # Id (Guid.CreateVersion7()), DomainEvents
│   │   ├── BaseAuditableEntity.cs        # CreatedAt/By, LastModifiedAt/By
│   │   └── BaseEvent.cs                  # INotification marker
│   ├── Entities/                          # Aggregate roots (pure C#, no annotations)
│   ├── Events/                            # Domain events (INotification)
│   ├── Enums/
│   └── Exceptions/
│
├── {Name}.Application/
│   ├── Common/
│   │   ├── Behaviors/                     # ValidationBehavior, LoggingBehavior, UnhandledExceptionBehavior
│   │   ├── Exceptions/                    # ValidationException, NotFoundException
│   │   └── Interfaces/                    # I{Name}DbContext, I{Name}ReadRepository, IUser
│   ├── {Feature}/Commands/{Cmd}/          # {Cmd}Command.cs, {Cmd}Handler.cs, {Cmd}Validator.cs
│   ├── {Feature}/Queries/{Qry}/           # {Qry}Query.cs
│   ├── Consumers/                         # IConsumer<T> — always InboxState for idempotency
│   └── DependencyInjection.cs
│
├── {Name}.Infrastructure/
│   ├── Data/
│   │   ├── {Name}DbContext.cs             # Implements I{Name}DbContext; Outbox/Inbox entities
│   │   ├── Configurations/                # IEntityTypeConfiguration<T> (Fluent API)
│   │   ├── Interceptors/                  # AuditableEntity, DispatchDomainEvents
│   │   └── Repositories/                  # Valkey read-model via IConnectionMultiplexer
│   ├── Services/                          # CurrentUser (JWT sub), adapters
│   └── DependencyInjection.cs
│
└── {Name}.Api/
    ├── Endpoints/                          # Minimal API mapping
    ├── Infrastructure/                     # EndpointExtensions, ProblemDetailsExceptionHandler
    └── Program.cs                          # builder.AddBasicServiceDefaults(), DI, middleware
```

### 6.1 Entity Conventions

- **PK:** `Guid` via `Guid.CreateVersion7()` — time-ordered UUID v7
- **Factory methods:** `static Create(...)` — no public constructors, `private set` on state
- **Domain events:** Raised via `AddDomainEvent()`, dispatched post-`SaveChanges` by interceptor (intra-process)
- **Integration events:** Published via `IPublishEndpoint.Publish()` through Transactional Outbox, after `SaveChangesAsync`

### 6.2 CQRS Split

- **Write (Commands):** MediatR → `DbContext` (Postgres) → Outbox publish
- **Read (Queries):** MediatR → Valkey read-model (`IXxxReadRepository`). Never read from write-side `DbContext`.

---

## 7. Messaging Architecture

### 7.1 Transport

| Property | Value |
|----------|-------|
| Broker | RabbitMQ 4.3.1-management (single node, MVP) |
| Client | MassTransit 8.5.4 (MIT), RabbitMQ transport |
| Topology | Choreography only. No saga orchestrator. No Kafka. |

### 7.2 Transactional Outbox

```csharp
x.AddEntityFrameworkOutbox<TDbContext>(o =>
{
    o.UsePostgres();
    o.UseBusOutbox();
    o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
});
```

`IPublishEndpoint.Publish()` atomic with `SaveChangesAsync` — message written to `OutboxMessage` in same transaction. **Never** call `Publish` before `SaveChanges`.

### 7.3 Consumer Idempotency

MassTransit `InboxState` for duplicate detection on every `IConsumer<T>`. Redelivered messages silently discarded.

### 7.4 Dead Letter

MassTransit auto-provisions `{queue}_error`. Non-zero depth = production incident. Monitor via RabbitMQ UI (`:15672`).

### 7.5 Domain Event Catalog

| Event | Producer | Consumer(s) | Sprint |
|-------|----------|-------------|--------|
| `OrderCreated` | Order | Payment | 1 |
| `StockReserved` | Inventory | Payment | 1 |
| `PaymentCompleted` | Payment | Fulfillment | 1 |
| `OrderShipped` | Fulfillment | Notification | 1, 6 |
| `PaymentFailed` | Payment | Order (cancel), Notification | 2, 6 |
| `OrderCancelled` | Order | Inventory (release) | 2 |
| `StockReleased` | Inventory | (terminal) | 2 |
| `UserRegistered` | IAM | Customer, Audit | 4, 5, 9 |
| `UserProvisioned` | IAM | Customer | 4, 5 |
| `ProductUpserted` | Catalog | Search | 5, 6 |
| `PermissionDenied` | IAM | Audit | 9 |
| `CouponWritten` | Promotion | Audit | 9 |

---

## 8. Synchronous Communication (gRPC)

### 8.1 Proto Contracts

All `.proto` files in `src/BuildingBlocks/ECommerce.Contracts/Protos/`.

| Proto | Server | RPC Methods | Callers |
|-------|--------|------------|---------|
| `inventory.proto` | Inventory | `ReserveStock`, `ReleaseStock` | Order |
| `iam.proto` | IAM | `CheckPermission` | Catalog, Customer, Promotion, Audit |
| `catalog.proto` | Catalog | `GetPriceSnapshot` | Order |
| `promotion.proto` | Promotion | `ApplyCoupon` | Order |
| `audit.proto` | Audit | `Verify` | Admin / Operator CLI |

### 8.2 gRPC Interceptors (ServiceDefaults)

- `GrpcJwtHeaderInterceptor` — forwards `Authorization: Bearer` → gRPC `authorization` metadata
- `GrpcTraceContextInterceptor` — propagates W3C `traceparent`

### 8.3 Resilience on gRPC Clients

```
Retry:          3 attempts, exponential backoff + jitter (200ms base)
CircuitBreaker: failure ratio 0.5, sampling 10s, break 30s
AttemptTimeout: 5s per attempt
TotalTimeout:   15s per call
```

### 8.4 Per-Dependency Fallback Policy (Sprint 8)

| Dependency | On breaker open |
|-----------|-----------------|
| Inventory `ReserveStock` | Fail checkout (stock correctness > availability) |
| Catalog `GetPriceSnapshot` | Fallback to last-cached Valkey price |
| Promotion `ApplyCoupon` | Fallback to no-discount (non-critical, fail open) |

---

## 9. Security Architecture

### 9.1 JWT — Defense in Depth

Every service validates JWTs independently via `JwtBearer`. Authority = Keycloak realm `ecommerce`. Gateway is NOT the sole trust boundary.

### 9.2 Rate Limiting (Gateway)

| Property | Value |
|----------|-------|
| Policy | Sliding window, 100 req/s per client IP |
| Backend | Valkey (`RedisRateLimiting`) |
| Fallback | In-memory `SlidingWindowLimiter` when Valkey unhealthy |
| Response | HTTP 429 |

### 9.3 Bulkhead (Gateway)

`Microsoft.Extensions.Http.Resilience` concurrency limiter: max 10 concurrent per backend cluster.

### 9.4 Idempotency Key Convention

`Idempotency-Key: <UUIDv7>` on all POST endpoints creating resources. `UNIQUE` constraint on resource table. Repeated key → original 201 response.

### 9.5 OWASP Mitigations

| OWASP | Mitigation |
|-------|-----------|
| API1 | Basket/Customer routes scoped to JWT `sub` |
| API3 | FluentValidation on all request DTOs |
| API4 | YARP rate limiter: 100 req/s per IP |
| API5 | Admin writes gated by IAM `CheckPermission` gRPC |
| A06 | `dotnet list package --vulnerable` — hard CI merge block |
| A08 | MassTransit fault → `_error` queue auto-routing |
| SAST | GitHub CodeQL — hard merge block on HIGH+ |

---

## 10. Observability

### 10.1 OpenTelemetry

`AddBasicServiceDefaults()` provides:
- **Metrics:** ASP.NET Core, HttpClient, Runtime, MassTransit, Npgsql
- **Traces:** ASP.NET Core, HttpClient, gRPC, MassTransit, Npgsql
- **Exporter:** OTLP → Aspire Dashboard (auto-wired by AppHost)

### 10.2 Structured Logging

Serilog 10.0.0. Every log carries `TraceId`/`SpanId`. **Never log PII** — only `OrderId`, `TrackingId`.

### 10.3 Custom Metrics

| Metric | Type | Service |
|--------|------|---------|
| `order.create_to_ship.duration` | Histogram | Fulfillment |
| `order.checkout.duration` | Histogram | Order |
| `iam.permission_check.duration` | Histogram | IAM |
| `catalog.price_snapshot.duration` | Histogram | Catalog |
| `customer.profile_sync.duration` | Histogram | Customer |
| `search.query.duration` | Histogram | Search |
| `notification.dispatch.count` | Counter | Notification |
| `promotion.coupon_apply.duration` | Histogram | Promotion |
| `saga.compensation.count` | Counter | Order |
| `audit.entry_ingest.duration` | Histogram | Audit |
| `audit.hash_chain.broken` | Counter | Audit |

### 10.4 SLOs

| Route / Process | p95 Latency / Lag | Error Rate / Target |
|-----------------|-------------------|--------------------|
| `POST /api/v1/orders` | < 300ms | < 1% |
| `GET /api/v1/inventory/{sku}/availability` | < 50ms (cached) | < 0.5% |
| `IAM CheckPermission` | < 100ms (Valkey 30s TTL) | < 1% |
| Audit Event Ingestion Lag | < 2s (event → DB write) | `audit.hash_chain.broken == 0` (Immediate Paging Alert) |

---

## 11. Infrastructure (Aspire AppHost)

`ECommerce.AppHost` = single source of truth for system topology.

| Resource | Image | Notes |
|----------|-------|-------|
| PostgreSQL | `postgres:18.4` | One DB per service, schema via `HasDefaultSchema` |
| RabbitMQ | `rabbitmq:4.3.1-management` | UI at `:15672` |
| Valkey | `valkey/valkey:9.1` | No AOF — disposable cache/lock state |
| Keycloak | `quay.io/keycloak/keycloak:26.6.4` | Generic `AddContainer`. Realm `ecommerce` via `realm-export.json` |
| Dashboard | Auto | Ephemeral OTel backend |

`aspire publish` → `docker-compose.yaml`. **Never hand-author** this file.

---

## 12. Persistence Conventions

### 12.1 EF Core

- Provider: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3
- `SetPostgresVersion(18, 0)` — UUID v7 native mapping
- Schema: `modelBuilder.HasDefaultSchema("{servicename}")`
- Dev: `EnsureCreatedAsync()`. Production: `MigrateAsync()`.

### 12.2 Outbox Schema

Every participating `DbContext`:
```csharp
modelBuilder.AddInboxStateEntity();
modelBuilder.AddOutboxMessageEntity();
modelBuilder.AddOutboxStateEntity();
```

### 12.3 Valkey Key Patterns

| Key | Purpose | TTL |
|-----|---------|-----|
| `basket:{KeycloakSubject}` | Hash (field=SKU, value=Qty) | 7d sliding |
| `idempotency:order:{key}` | String → OrderId | 24h |
| `iam:perm:{subject}:{permission}` | IDistributedCache | 30s |
| `lock:basket:{sub}` | DistributedLock mutex | ~5s auto-extend |

---

## 13. API Design

### 13.1 URL

`/api/v1/{resource}/{id?}` — `Asp.Versioning.Http` (`UrlSegmentApiVersionReader`).

### 13.2 Errors

All 4xx/5xx → `application/problem+json` (RFC 9457). `AddProblemDetails()` + `AddExceptionHandler<ProblemDetailsExceptionHandler>()`. Never plain string errors.

### 13.3 Key Headers

| Header | Direction | Usage |
|--------|-----------|-------|
| `Authorization: Bearer <token>` | Inbound | Keycloak JWT |
| `Idempotency-Key: <UUIDv7>` | Inbound | POST create endpoints |
| `authorization` (lowercase) | gRPC metadata | JWT forwarding |
| `traceparent` | gRPC / RabbitMQ | W3C TraceContext |

---

## 14. Data Models by Service

### 14.1 Order

| Table | Key Columns |
|-------|------------|
| `Orders` | OrderId PK, IdempotencyKey UNIQUE, CustomerId, KeycloakSubject, Status, TotalAmount |
| `OrderItems` | OrderId FK, Sku, Qty, UnitPrice (snapshotted from Catalog) |
| Basket | Valkey Hash `basket:{sub}` — NOT in Postgres |

### 14.2 Inventory

| Table | Key Columns |
|-------|------------|
| `InventoryReservations` | ReservationId PK, OrderId, Sku, Qty, Status (Active/Released) |

### 14.3 Payment

| Table | Key Columns |
|-------|------------|
| `Payments` | PaymentId PK, OrderId, IdempotencyKey UNIQUE, Amount, Status |

### 14.4 Fulfillment (deepened Sprint 7)

| Table | Key Columns |
|-------|------------|
| `Shipments` | OrderId PK, TrackingNumber UNIQUE, Status, ShippedAt |

### 14.5 IAM

| Table | Key Columns |
|-------|------------|
| `IamProfiles` | KeycloakSubject PK, DisplayName, Email, Status (PendingIdentity→Active) |
| `Invitations` | Id PK, Email, Role, Status, ExpiresAt, IdempotencyKey UNIQUE |
| `GroupMemberships` | KeycloakSubject, GroupId |

### 14.6 Catalog

| Table | Key Columns |
|-------|------------|
| `Products` | Id PK, Sku UNIQUE, Name, Description, BrandId, CategoryId |
| `Categories`, `Brands` | Standard lookup |
| `Variants` | ProductId, Sku, Attributes (jsonb) |
| `Images` | ProductId, Url, SortOrder |

### 14.7 Customer

| Table | Key Columns |
|-------|------------|
| `Customers` | KeycloakSubject PK, DisplayName, Email |
| `Addresses` | CustomerId FK, Line1, City, PostalCode |
| `Preferences` | CustomerId FK, Key, Value |

### 14.8 Search

| Table | Key Columns |
|-------|------------|
| `SearchIndex` | Sku PK, Name, Description, SearchVector (tsvector GENERATED, GIN index) |

### 14.9 Promotion

| Table | Key Columns |
|-------|------------|
| `Coupons` | Code UNIQUE, DiscountType, Value, ExpiresAt |
| `Campaigns` | Standard campaign entity |

### 14.10 Audit (Sprint 9)

Append-only, tamper-evident compliance audit trail microservice. Consumes security- and compliance-relevant domain events asynchronously (zero impact on write hot paths).

#### 14.10.1 Schema & Data Model

| Table | Key Columns & Constraints |
|-------|---------------------------|
| `AuditEntries` | `Id` PK `bigserial`, `ActorSubject` (Guid/String), `Action` (String), `TargetType` (String), `TargetId` (String), `Outcome` (String: Success/Denied/Failed), `OccurredAt` (Timestamptz), `RowHash` (Char 64 Hex), `PrevHash` (Char 64 Hex), `IdempotencyKey` `Guid` UNIQUE |
| `InboxState` | MassTransit consumer dedup table (`MessageId`, `ConsumerId`, `Received`) |

#### 14.10.2 Tamper-Evident Hash Chain Algorithm

Every `AuditEntries` record includes a cryptographic SHA-256 hash chained to the preceding row's `RowHash`:

$$\text{RowHash}_n = \text{SHA256}(\text{PrevHash}_{n-1} \parallel \text{ActorSubject} \parallel \text{Action} \parallel \text{TargetType} \parallel \text{TargetId} \parallel \text{Outcome} \parallel \text{OccurredAtIso8601})$$

- **Genesis Block:** Initial row's `PrevHash` is initialized to `64` zeros (`"0" * 64`).
- **Atomic Ingestion:** `PrevHash` lookup, `RowHash` computation, `AuditEntries` append, and MassTransit `InboxState` check-and-insert execute in the **same DB transaction** under a table-level append lock (`pg_advisory_xact_lock` / serializable write) to guarantee zero hash chain divergence under high event concurrency.

#### 14.10.3 DB Immutability Hardening

PostgreSQL schema migration applies strict access control revoking mutation capabilities from the application user:

```sql
REVOKE UPDATE, DELETE ON TABLE audit."AuditEntries" FROM app_role;
```

EF Core DbContext configuration additionally overrides entity state tracking to throw an exception if `EntityState.Modified` or `EntityState.Deleted` is attempted.

#### 14.10.4 API & gRPC Surfaces

- **REST Query API:** `GET /api/v1/audit/entries?actor={sub}&action={act}&from={iso}&to={iso}&cursor={id}&limit=50`
  - **Security:** Gated by IAM `CheckPermission` gRPC, requires `ADMIN` role.
  - **Pagination:** Cursor-based (`Id > cursor`), ordered by `Id ASC`.
  - **Immutability:** Pure read-only endpoint (no `POST`, `PUT`, `PATCH`, or `DELETE` endpoints exist).
- **gRPC Integrity Verification:** `AuditService.Verify(VerifyRequest)` → `VerifyResponse`
  - RPC method: `rpc Verify (VerifyRequest) returns (VerifyResponse);`
  - Iterates sequentially from `from_id` to `to_id`, re-calculating `SHA256` hash for each entry.
  - Returns `valid = false`, `broken_at_id`, and `error_message` upon detecting any hash mismatch or chain break.

#### 14.10.5 Event Ingestion Catalog

| Event | Producer | Audited Action Payload Mapping |
|-------|----------|--------------------------------|
| `PermissionDenied` | IAM.Api | Action: `"IAM.PermissionDenied"`, TargetType: `"Permission"`, TargetId: `{Permission}`, Outcome: `"Denied"` |
| `CouponWritten` | Promotion.Api | Action: `"Promotion.CouponWritten"`, TargetType: `"Coupon"`, TargetId: `{Code}`, Outcome: `"Success"` |
| `UserRegistered` | IAM.Api | Action: `"IAM.UserRegistered"`, TargetType: `"User"`, TargetId: `{KeycloakSubject}`, Outcome: `"Success"` |

---

## 15. Sprint Delivery Map

| Sprint | Goal | Services | Key Deliverables |
|--------|------|----------|-----------------|
| **1** | E2E happy path: JWT → basket → order → reserve → pay → ship | Gateway, Order, Inventory, Payment, Fulfillment | Keycloak auth, Basket (Valkey), gRPC reserve, MassTransit Outbox, 3 events |
| **2** | Compensation: payment failure → cancel → release stock | Order, Inventory, Payment | PaymentFailed/OrderCancelled/StockReleased, InboxState dedup, UseMessageRetry |
| **3** | Cross-cutting hardening: trace correlation, rate limiting, CI/CD | All 5 | W3C TraceContext gRPC interceptor, RedisRateLimiting, bulkhead, GitHub Actions, CodeQL |
| **4** | IAM.Api: registration lifecycle + permission façade | Gateway, IAM | CheckPermission gRPC, UserRegistered, Keycloak Admin API, IdentityProvisioningWorker |
| **5** | Catalog.Api + Customer.Api | Gateway, Catalog, Customer | GetPriceSnapshot gRPC, ProductUpserted, UserRegistered consumer, OutputCache |
| **6** | Search.Api + Notification.Api | Gateway, Search, Notification | tsvector read-model, ProductUpserted consumer, OrderShipped/PaymentFailed consumers |
| **7** | Promotion.Api + Fulfillment deepening + cross-cutting closure | Gateway, Promotion, Fulfillment, all 11 | ApplyCoupon gRPC, Shipments table, API versioning, ProblemDetails, PactNet 7 contracts |
| **9** | Audit.Api: compliance audit trail | Gateway, Audit, IAM, Promotion | AuditEntries (SHA-256 chained), DB REVOKE UPDATE/DELETE, AuditService.Verify gRPC, GET /audit/entries |

---

## 16. Testing Architecture

### 16.1 Unit Tests

- **xUnit.v3 3.2.2** + **Shouldly 4.3.0** + **Moq 4.20.72**
- Scope: Domain logic + Application handlers. Zero infrastructure deps.

### 16.2 Integration Tests

- **Testcontainers 4.13.0** (`PostgreSql`, `RabbitMq`, Keycloak module)
- Require Docker daemon. Cover: happy-path saga, compensation, idempotency, JWT 401.

### 16.3 Contract Tests

- **PactNet 4.5.0** — file-based, broker-free
- 7 contracts for edges proto-versioning doesn't cover:

| Contract | Type |
|----------|------|
| Gateway ↔ Order | REST |
| Gateway ↔ Catalog | REST |
| Gateway ↔ Search | REST |
| Notification ↔ `OrderShipped` | Event |
| Notification ↔ `PaymentFailed` | Event |
| Search ↔ `ProductUpserted` | Event |
| Customer ↔ `UserRegistered` | Event |

## 17. CI/CD

Path-filtered GitHub Actions per service:

```yaml
on:
  push:
    paths: ['src/Services/{ServiceName}/**']
```

Pipeline:
```
dotnet list package --vulnerable    # Hard block HIGH+
CodeQL (codeql-action@v3)           # Hard block CRITICAL/HIGH
dotnet test (unit)
Testcontainers integration
docker build (sdk:10.0 → aspnet:10.0-alpine)
docker push → GHCR
```

---

## 18. YAGNI — Explicitly Rejected

| Item | Reason |
|------|--------|
| ArchUnitNET | Onion enforced by `.csproj` reference graph — compiler catches violations |
| `Aspire.Hosting.Keycloak` | Still preview. Generic `AddContainer` on stable API. |
| Kafka / KRaft | No partition-ordered log need. RabbitMQ Outbox fully supported on MT v8. |
| Redis | Valkey: BSD-3-Clause, ~20% lower memory, wire-compatible. |
| Native AOT | EF Core AOT partial in .NET 10. Deferred. |
| Prometheus + Alertmanager | Aspire Dashboard sufficient for MVP. Add at team-scale. |
| Kubernetes / Helm | Out of scope. `docker compose up` is MVP target. |
| MassTransit v9+ | Commercial license. v8.5.4 MIT covers all requirements. |
| SonarQube / Snyk | CodeQL + `dotnet list package --vulnerable` free at this scale. |
| Redlock multi-master | Single Valkey. `DistributedLock.Redis` single-instance mutex correct here. |
| FluentAssertions v8+ | Paid Xceed license. Shouldly 4.3.0 (MIT) chosen. |
| Review / Wishlist services | No consumer justifies them. Same YAGNI logic as other cuts. |
| Trivy + CycloneDX SBOM | CodeQL + SCA pair sufficient at MVP. Reintroduce for compliance. |

---

## 19. Agent Rules

### 19.1 Adding a Feature

1. **Domain** → entity. No framework deps.
2. **Application/Common/Interfaces** → interface if infra changes needed.
3. **Application/{Feature}** → Command/Query handler + validator.
4. **Infrastructure** → implement the interface.
5. **Api/Endpoints** → Minimal API endpoint.
6. Never inject `DbContext` into an endpoint — always through MediatR.

### 19.2 Adding an Integration Event

1. Add `record` to `ECommerce.Contracts/Events/v1/`.
2. Publish via `IPublishEndpoint.Publish()` inside Command handler — atomic with `SaveChangesAsync`.
3. Add `IConsumer<T>` to `Application/Consumers/` in consuming service.
4. Register via `x.AddConsumer<T>()` in Infrastructure `AddMassTransit`.
5. Verify `InboxState` active on consumer's `DbContext`.
6. Update §7.5 Domain Event Catalog.

### 19.3 Adding a gRPC Contract

1. Add `.proto` to `ECommerce.Contracts/Protos/`.
2. Implement service in `{Name}.Api/Services/`. Register `app.MapGrpcService<T>()`.
3. `AddGrpcClient<T>()` in client Infrastructure → chain both interceptors + `.AddStandardResilienceHandler()`.

### 19.4 Adding a New Microservice

1. Create four Onion projects: Domain, Application, Infrastructure, Api.
2. Verify Domain has zero framework `PackageReference`.
3. Add `{name}_db` database in `AppHost/Program.cs`.
4. Register service in AppHost + YARP route in `ECommerce.Gateway/appsettings.json`.
5. Add `ProjectReference` to AppHost `.csproj`.
6. Add path-filtered CI workflow `.github/workflows/{name}-api.yml`.
7. Add Dockerfile (required for Aspire container publish).

### 19.5 What Agents Must NEVER Do

- Add `Microsoft.EntityFrameworkCore` to any `*.Domain.csproj`
- Add `ProjectReference` from `*.Domain` to `*.Infrastructure` or `*.Api`
- Share a `DbContext` type between two services
- Upgrade `MassTransit` beyond `8.5.4`
- Replace `valkey/valkey:9.1` with `redis:*` in AppHost
- Read from `DbContext` in a Query handler — use the Valkey read repository
- Log PII — only `OrderId`, `TrackingId`, never emails/names/phones
- Call `db.Database.MigrateAsync()` without a migration plan — `EnsureCreatedAsync` is dev only
- Call `IPublishEndpoint.Publish()` outside a `DbContext`-backed Command handler without idempotency guard
- Hand-edit `docker-compose.yml` — it is generated by `aspire publish`
