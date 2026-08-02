// OrderCreatedConsumer removed: stock reservation is now handled synchronously via gRPC
// from Order.Api (CreateOrderCommand calls InventoryService.ReserveStockAsync before publishing OrderCreated).
// Plan: Order.Api —gRPC→ Inventory.Api (ReserveStock, fail-fast) — sync, not event-driven.
// Inventory.Api still listens to OrderCancelled (for compensation) and PaymentFailed.
namespace Inventory.Application.Consumers;
