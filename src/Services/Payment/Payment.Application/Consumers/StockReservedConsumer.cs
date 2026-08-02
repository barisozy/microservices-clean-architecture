// This file is intentionally left empty.
// StockReservedConsumer was removed in saga flow refactor.
// Payment.Api now consumes OrderCreated directly (see OrderCreatedConsumer.cs).
// Plan: Order.Api calls Inventory.Api via gRPC synchronously, then publishes OrderCreated.
namespace Payment.Application.Consumers;
