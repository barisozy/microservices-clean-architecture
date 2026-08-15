# Event Topology

| Service | Publishes | Consumes |
|---------|-----------|----------|
| **Audit** | - | `CouponWritten, PermissionDenied, UserRegistered` |
| **Catalog** | `ProductUpserted` | - |
| **Customer** | - | `UserRegistered` |
| **Fulfillment** | - | `PaymentCompleted` |
| **IAM** | `PermissionDenied, UserProvisioned, UserRegistered` | - |
| **Inventory** | `StockReleased` | `OrderCancelled, PaymentFailed` |
| **Notification** | - | `OrderShipped, PaymentFailed` |
| **Order** | `Commit, Order, OrderCancelled, OrderCheckoutCompleted, ProcessPayment, RefundPayment, Release, Reserve` | `OrderCancelled, OrderShipped, PaymentCompleted, PaymentFailed, StockReleased` |
| **Payment** | `PaymentCompleted, PaymentFailed` | `ProcessPayment, RefundPayment` |
| **Promotion** | - | - |
| **Search** | - | `ProductUpserted` |