# System Design Documentation

## 1. Architecture Overview
The system consists of 3 main microservices focused on "E-Commerce Order Management", built with **.NET 9** and **CQRS / MediatR** pattern:
*   **Order Service:** Manages the order lifecycle (Pending -> Confirmed -> Shipped -> Delivered) and Idempotency logic.
*   **Inventory Service:** Manages product stocks, temporary reservations, and business rules (e.g., maximum 50% stock reservation per order).
*   **Payment Service:** A mock service managing payment simulations (success, timeouts, failures, fraud detection) and retry/reversal operations.

### Key Architectural Patterns
*   **CQRS with MediatR:** Each microservice implements Command Query Responsibility Segregation, utilizing MediatR to separate business logic (Commands/Queries) from controllers.
*   **Event-Driven & Saga Pattern:** Communication between services is highly asynchronous over **RabbitMQ (via MassTransit)**. Distributed Transactions are handled via a Choreography-based Saga pattern. If an error occurs at any step (e.g., Payment fails), "Compensating Transactions" run to release reserved stock and cancel the order.
*   **Global Exception Handling:** A centralized `GlobalExceptionHandler` ensures all domain exceptions and validation errors are correctly mapped to RFC-7807 standard `ProblemDetails` responses.

*For a complete view of the Saga and messaging steps, refer to [Event Architecture Flow](./event-architecture-flow.md).*

## 2. Database Design Decisions
Each microservice has its own isolated database schema (using PostgreSQL), following the **Database-per-Service** pattern:

*   **Order DB (`ecommerce_order_db`):**
    *   `Orders`: Order details (CustomerId, Status, TotalAmount, IdempotencyKey).
        *   **Decision:** A unique index has been added for the `IdempotencyKey` field to provide "duplicate order prevention" and idempotency support.
    *   `OrderItems`: Products and their quantities within the order.
*   **Inventory DB (`ecommerce_inventory_db`):**
    *   `Products`: Product details (Id, Name, TotalStock, RowVersion).
    *   `StockReservations`: Instant reservations based on the order (OrderId, ProductId, ReservedQuantity, ExpiresAt).
        *   **Decision:** To prevent race conditions during concurrent bulk stock updates, `RowVersion` based **Optimistic Locking** is used on the EF Core side (ConcurrencyToken).
*   **Payment DB (`ecommerce_payment_db`):**
    *   `Payments`: Payment records (OrderId, Amount, Status, Method).

## 3. Resilience and Fault Tolerance
*   **Polly:** Utilized for Payment simulation retries and handling exponential backoffs.
*   **MassTransit Retries & DLQ:** Consumer pipelines are configured with retry policies (e.g., retrying 3 times over 5 seconds) before moving failing messages to a Dead Letter Queue (DLQ).
*   **Database Retry on Failure:** EF Core is configured with `.EnableRetryOnFailure()` to seamlessly recover from transient network drops or PostgreSQL unavailability.

## 4. Observability
*   **Distributed Tracing:** OpenTelemetry is integrated across all microservices. Spans are exported to **Jaeger** via OTLP, enabling end-to-end tracking of HTTP requests, database transactions, and RabbitMQ messages.
*   **Metrics:** Standard runtime metrics and custom business metrics (e.g., `orders_created_count`, `revenue_total`) are exposed via `/metrics` endpoints for **Prometheus** scraping.
*   **Structured Logging:** **Serilog** is configured centrally in the `Shared` project, writing structured JSON/Console logs that include critical properties like `CorrelationId` and trace context.
