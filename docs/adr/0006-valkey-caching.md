# ADR 0006: Valkey for Distributed Caching

## Status
Accepted

## Context
Certain data structures, such as the user's shopping basket (cart), are ephemeral and frequently updated. Storing them in a relational database adds unnecessary read/write pressure. Additionally, we need a caching layer to improve response times for read-heavy operations. While Redis has been the standard, its recent licensing changes make it less attractive for open-source and enterprise usage.

## Decision
We will use **Valkey** (a truly open-source, BSD-3-Clause fork of Redis) as the distributed caching provider. Specifically, the Basket service and API Gateways will use Valkey as their primary data store to achieve high read/write throughput and caching capabilities while remaining fully compatible with existing Redis client libraries (like `StackExchange.Redis`).

## Consequences
**Positive:**
- Extremely fast read/write operations suitable for shopping carts.
- Reduces load on relational databases for frequently accessed, non-critical data.
- Built-in data expiration (TTL) handles stale carts automatically.

**Negative:**
- Adds another piece of infrastructure to maintain and monitor.
- Requires robust serialization/deserialization logic for objects stored in cache.
