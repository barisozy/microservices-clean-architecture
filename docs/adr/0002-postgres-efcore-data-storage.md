# ADR 0002: PostgreSQL and Entity Framework Core for Data Storage

## Status
Accepted

## Context
Each microservice requires a reliable relational database to store its state (Orders, Stock, Payments, Shipments, Catalog data and AuditEntries). We need an ORM that is fully supported by .NET 10 to interact with the database efficiently while supporting modern patterns like interceptors.

## Decision
We will use **PostgreSQL 18.4** as the primary relational database and **Entity Framework Core 10** as the ORM. Each microservice owns exactly one schema and DbContext; cross-schema joins and shared tables are prohibited.

## Consequences
**Positive:**
- PostgreSQL is open-source, highly performant, and supports advanced JSONB operations if needed.
- EF Core integrates seamlessly with .NET 10 and provides strongly typed data access.
- Interceptors in EF Core make it easy to automatically manage audit fields (CreatedBy, LastModifiedBy) and dispatch Domain Events.

**Negative:**
- Requires managing EF Core migrations as part of the CI/CD pipeline.
- Developers must be mindful of N+1 query problems and EF Core performance tuning.
