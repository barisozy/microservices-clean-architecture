# ADR 0006: Valkey for Distributed Caching

## Status
Accepted

## Context
Certain data structures, such as the user's shopping basket (cart), are ephemeral and frequently updated. Storing them in a relational database adds unnecessary read/write pressure. Additionally, we need a caching layer to improve response times for read-heavy operations. While Redis has been the standard, its recent licensing changes make it less attractive for open-source and enterprise usage.

## Decision
We will use **Valkey 9.1** (BSD-3-Clause) as the distributed caching provider. The Order service owns the basket hash; services also use Valkey-backed read models, locks and Gateway rate-limit state. Valkey remains compatible with `StackExchange.Redis`.

## Consequences
**Positive:**
- Extremely fast read/write operations suitable for shopping carts.
- Reduces load on relational databases for frequently accessed, non-critical data.
- Built-in data expiration (TTL) handles stale carts automatically.

**Negative:**
- Adds another piece of infrastructure to maintain and monitor.
- Requires robust serialization/deserialization logic for objects stored in cache.
