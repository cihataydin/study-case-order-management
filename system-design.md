# System Design Documentation

## 1. Architecture Overview
The system consists of 3 main microservices focused on "E-Commerce Order Management":
*   **Order Service:** Manages the order lifecycle (Pending -> Confirmed -> Shipped, etc.). 
*   **Inventory Service:** Stores product stocks, reserves stock (Stock Reservation), and manages a 10-minute timeout mechanism.
*   **Payment Service:** Manages the payment simulation and retry/reversal operations in case of failure.

**Event-Driven & Saga Pattern:** 
Communication between services will be provided asynchronously over **RabbitMQ**. For Distributed Transaction management, a Choreography or Orchestration-based Saga pattern will be applied. When an order is created, stock will be reserved and payment will be taken sequentially. If an error occurs at any step, "Compensating Transactions" will run (e.g., if payment fails, stock will be released and the order will be canceled).

## 2. Database Design Decisions
Each microservice has its own isolated database schema (using PostgreSQL):

*   **Order DB:**
    *   `Orders`: Order details (CustomerId, Status, TotalAmount, IdempotencyKey).
        *   **Decision:** A unique index has been added for the `IdempotencyKey` field to provide "duplicate order prevention".
    *   `OrderItems`: Products and their quantities within the order.
*   **Inventory DB:**
    *   `Products`: Product details (Id, Name, TotalStock).
    *   `StockReservations`: Instant reservations based on the order (OrderId, ProductId, ReservedQuantity, ExpiresAt).
        *   **Decision:** To prevent race conditions, `RowVersion` based **Optimistic Locking** will be used on the EF Core side (ConcurrencyToken).
*   **Payment DB (Mock):**
    *   `Payments`: Payment records (OrderId, Amount, Status, Method).

## 3. API Contracts (OpenAPI/Swagger)
All services have OpenAPI/Swagger support. Once the services are running, the current contracts and testing interface can be accessed via the browser using the links below:

*   **Order Service:** [http://localhost:5277/swagger/index.html](http://localhost:5277/swagger/index.html)
*   **Inventory Service:** [http://localhost:5050/swagger/index.html](http://localhost:5050/swagger/index.html)
*   **Payment Service:** [http://localhost:5032/swagger/index.html](http://localhost:5032/swagger/index.html)
