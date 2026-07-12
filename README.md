# E-Commerce Order Management System

This project is a simulated Order Management System developed in accordance with microservices architecture and an event-driven communication model.

## ✨ Key Features & Business Rules
*   **Idempotency:** Unique `X-Idempotency-Key` implementation preventing duplicate order submissions.
*   **Optimistic Locking:** Concurrent requests updating stock are handled safely using EF Core `RowVersion`.
*   **Stock Rules:** Cannot reserve more than 50% of available stock per order. Temporary stock reservations automatically expire after 10 minutes.
*   **Resilience & Retries:** Payment Service utilizes **Polly** for exponential backoffs, simulating varying network scenarios (85% success, 10% timeout, 5% failure).
*   **Choreography Saga Pattern:** RabbitMQ events manage the lifecycle of the order. If a payment fails, compensating transactions are dispatched to release the stock and cancel the order.

For detailed architecture diagrams and system decisions, please read:
*   [System Design Documentation](./system-design.md)
*   [Event Architecture Flow](./event-architecture-flow.md) (Contains Mermaid Sequence Diagrams)

## 🏗 Project Structure
*   **OrderService.Api:** Service managing the order processes. (Port: `5277`)
*   **InventoryService.Api:** Service managing product stocks and stock reservation processes. (Port: `5050`)
*   **PaymentService.Api:** Mock service managing payment and refund processes. (Port: `5032`)
*   **Shared:** A shared library containing common classes, events, exception handling, logging configuration, and message definitions used for communication between microservices.

---

## 🚀 Getting Started & Installation

### 1. Running Infrastructure Only (Local Development)
If you want to run only the required infrastructure (PostgreSQL, Redis, RabbitMQ, Jaeger, etc.) and run the APIs locally via IDE/CLI:

```bash
docker compose up -d
```


### 2. Running the Entire Stack (Infrastructure + APIs) via Docker
You can run the entire system, including the microservices, entirely inside Docker containers without needing the .NET SDK installed on your host machine.

```bash
docker compose --profile apps up -d --build
```

This process will run:
*   **PostgreSQL:** `localhost:5432`
*   **RabbitMQ:** `localhost:5672` (AMQP) and `localhost:15672` (Management)
*   **Redis:** `localhost:6379`
*   **Jaeger:** `localhost:16686`
*   **Order Service:** `http://localhost:5277/swagger`
*   **Inventory Service:** `http://localhost:5050/swagger`
*   **Payment Service:** `http://localhost:5032/swagger`

To stop all services including the APIs:
```bash
docker compose --profile apps down
```

### 3. Running the Microservices Locally (.NET CLI)
You can run each service in independent terminal windows by navigating to their respective directories or using the following commands from the root directory:

#### A. Order Service (Port: 5277)
```bash
dotnet run --project src/OrderService.Api/OrderService.Api.csproj
```
Swagger UI: [http://localhost:5277/swagger/index.html](http://localhost:5277/swagger/index.html)

#### B. Inventory Service (Port: 5050)
```bash
dotnet run --project src/InventoryService.Api/InventoryService.Api.csproj
```
Swagger UI: [http://localhost:5050/swagger/index.html](http://localhost:5050/swagger/index.html)

#### C. Payment Service (Port: 5032)
```bash
dotnet run --project src/PaymentService.Api/PaymentService.Api.csproj
```
Swagger UI: [http://localhost:5032/swagger/index.html](http://localhost:5032/swagger/index.html)

---

## 📬 Postman & API Interaction
You can find the ready-to-use Postman/Insomnia collection in the root directory:
**`ECommerce_Order_Management.postman_collection.json`**

Import this file into your API client to easily test the endpoints (e.g., POST `/api/v1/orders`) without using Swagger.

### 💡 Tip for Testing (Seeded Products)
To create an order, you will need valid `productId`s. The **Inventory Service** automatically seeds 50 dummy products into the database when it first runs. You can use the following predictable GUIDs to test your order creation payload:
*   **Product 1:** `11111111-1111-1111-1111-000000000001`
*   **Product 2:** `11111111-1111-1111-1111-000000000002`
*   ...
*   **Product 50:** `11111111-1111-1111-1111-000000000050`

---

## 📝 Database and Migrations
When the services are run for the first time, they automatically create their databases (`ecommerce_order_db`, `ecommerce_inventory_db`, and `ecommerce_payment_db`) inside PostgreSQL and migrate their schemas.

If you want to apply migrations manually or add a new migration:

```bash
# Example: Creating a new migration for the Order Service
cd src/OrderService.Api
dotnet ef migrations add <MigrationName> -o Infrastructure/Data/Migrations

# Updating the database
dotnet ef database update
```

---

## 🔒 Security and Configuration Note
To ensure the local development and evaluation process is **plug-and-play**, the PostgreSQL and RabbitMQ connection details for docker-compose are defined by default in the `appsettings.json` files. 

In real/production environments, it is recommended to store this sensitive data in files excluded from the repository (such as `appsettings.Development.json`), as **Environment Variables**, or in secure external secret stores. The `.gitignore` file in this project is configured according to these standards.

---

## 📊 Testing Observability & Resilience

We have integrated production-ready observability and fault-tolerance mechanisms into the microservices. Here is how you can test and view them:

### 1. Distributed Tracing (Jaeger)
OpenTelemetry is configured to trace requests across `Order`, `Inventory`, and `Payment` services, including RabbitMQ messaging.
*   **Access Jaeger UI:** After running `docker compose up -d` and the microservices, go to [http://localhost:16686](http://localhost:16686).
*   **Test:** Create an order. In Jaeger, select the `OrderService.Api` service and click **Find Traces**. You will see a detailed waterfall graph showing the HTTP request, database insertions, and RabbitMQ message publishing/consuming across all three microservices.

### 2. Custom Metrics (Prometheus)
Each service exposes runtime and HTTP metrics. The Order Service also exposes custom business metrics.
*   **Access Metrics:**
    *   Order Service: [http://localhost:5277/metrics](http://localhost:5277/metrics)
    *   Inventory Service: [http://localhost:5050/metrics](http://localhost:5050/metrics)
    *   Payment Service: [http://localhost:5032/metrics](http://localhost:5032/metrics)

### 3. Global Exception Handling & Resilience
*   **Validation Errors (400 Bad Request):** Send an empty `POST /api/v1/orders` request via Swagger. You will receive an RFC-7807 compliant `ProblemDetails` JSON.
*   **Domain Exceptions & Conflicts (409 Conflict):** Due to strict business rules and Optimistic Locking, violations will return clear, structured 400 or 409 responses handled centrally by the `GlobalExceptionHandler`.
*   **Network Faults (Polly & MassTransit):** MassTransit is configured with `.UseMessageRetry()`. If a consumer fails, it automatically retries before sending the message to a DLQ.

---

## 🧪 Running Tests

The solution contains a comprehensive suite of unit tests and Testcontainers-based integration tests.

### 1. Running All Tests
To run all tests in the solution (including unit and integration tests):
```bash
dotnet test
```

### 2. Running Unit Tests Only
You can run the unit tests for individual services by targeting their test projects:
```bash
# Order Service Unit Tests
dotnet test tests/OrderService.Api.UnitTests/OrderService.Api.UnitTests.csproj

# Inventory Service Unit Tests
dotnet test tests/InventoryService.Api.UnitTests/InventoryService.Api.UnitTests.csproj

# Payment Service Unit Tests
dotnet test tests/PaymentService.Api.UnitTests/PaymentService.Api.UnitTests.csproj
```

### 3. Running Integration Tests
Integration tests use **Testcontainers** to spin up real PostgreSQL containers. Ensure Docker is running, then run:
```bash
dotnet test tests/OrderService.Api.IntegrationTests/OrderService.Api.IntegrationTests.csproj
```
