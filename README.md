# E-Commerce Microservices Platform

Production-oriented .NET 10 microservices reference platform. It uses independently deployable services, per-service PostgreSQL schemas, RabbitMQ/MassTransit integration events, gRPC for synchronous calls, Valkey for disposable read models and Aspire for local orchestration.

## Architecture

Every backend uses Onion Architecture:

```text
Domain -> Application -> Infrastructure -> Api
```

The compiler-enforced project reference graph keeps Domain free of EF Core, MassTransit, gRPC and ASP.NET Core dependencies. Services never reference each other directly; shared compiled code is limited to `ECommerce.Contracts`, `ECommerce.ServiceDefaults`, and the cross-cutting `ECommerce.Auditing` interceptor package.

Each service owns its PostgreSQL schema. Cross-service access is only through gRPC, integration events, or local read models.

## Services

| Service | Responsibility |
|---|---|
| Gateway | YARP routing, JWT validation, Valkey-backed rate limiting |
| Order | Orders, basket, checkout, compensation |
| Inventory | Stock reservations and availability |
| Payment | Mock payment processing and failure simulation |
| Fulfillment | Shipment creation and tracking |
| IAM | Keycloak administration, invitations and permissions |
| Catalog | Products, categories, variants and price snapshots |
| Customer | Profiles, addresses and preferences |
| Search | PostgreSQL full-text search read model |
| Notification | Event-driven mock notifications |
| Promotion | Coupons, campaigns and discount calculation |
| Audit | Append-only compliance trail with SHA-256 hash-chain verification |

`ECommerce.Auditing` is not the Audit microservice. It is a shared EF Core `SaveChangesInterceptor` that maintains ordinary entity audit fields.

## Order flow

```text
Order --gRPC--> Inventory ReserveStock
Order --OrderCreated--> Payment --PaymentCompleted--> Fulfillment --OrderShipped--> Notification

Payment --PaymentFailed--> Order --OrderCancelled--> Inventory --StockReleased-->
```

Every participating service uses MassTransit’s EF Core transactional outbox and inbox. For PostgreSQL, outbox registration disables schema caching (`UsePostgres(enableSchemaCaching: false)`) so hosts with multiple DbContexts, including integration tests, always use the correct service schema.

## Local development

Prerequisites: .NET SDK specified by `global.json` and Docker Desktop.

```powershell
dotnet run --project src/Orchestration/ECommerce.AppHost
```

Aspire AppHost is the source of truth for local topology. It starts PostgreSQL 18.4, RabbitMQ 4.3.1, Valkey 9.1, Keycloak and the platform services. Do not hand-edit generated compose output.

## Tests

```powershell
# Unit tests
dotnet test ECommerce.UnitTests.slnf --maxcpucount:1

# Integration tests; Docker must be running
dotnet test ECommerce.IntegrationTests.slnf --maxcpucount:1

# Consumer/provider contracts
dotnet test ECommerce.ContractTests.slnf --maxcpucount:1

# Full solution build
dotnet build ECommerce.sln --maxcpucount:1
```

Integration coverage includes the Order → Payment → Fulfillment path, payment-failure compensation, stock release, JWT authentication, transactional outbox delivery and inbox deduplication.

## Technology baseline

- .NET 10 / C# 14
- PostgreSQL 18.4 and EF Core 10
- RabbitMQ 4.3.1 with MassTransit 8.5.4
- Valkey 9.1 with StackExchange.Redis
- Keycloak 26.6.4
- YARP 2.3.0, gRPC 2.80.0, OpenTelemetry and Serilog
- xUnit v3, Shouldly and Testcontainers

MassTransit remains pinned to 8.5.4 and Valkey must not be replaced with Redis.

See [architecture decisions](docs/adr/) and the [test guide](docs/testing.md) for operational detail.
