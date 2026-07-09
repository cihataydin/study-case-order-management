# E-Commerce Order Management System

This project is a simulated Order Management System developed in accordance with microservices architecture and an event-driven communication model.

## 🏗 Project Structure
*   **OrderService.Api:** Service managing the order processes. (Port: `5277`)
*   **InventoryService.Api:** Service managing product stocks and stock reservation processes. (Port: `5050`)
*   **PaymentService.Api:** Mock service managing payment and refund processes. (Port: `5032`)
*   **Shared:** A shared library containing common classes, events, and message definitions used for asynchronous communication between microservices.

---

## 🚀 Getting Started & Installation

### 1. Starting Infrastructure Services (Docker)
You can start the PostgreSQL and RabbitMQ services required by the project with a single command via Docker:

```bash
docker compose up -d
```

This process will run:
*   **PostgreSQL:** On port `localhost:5432` with credentials `root` / `rootpassword`.
*   **RabbitMQ:** On port `localhost:5672` (AMQP) and `localhost:15672` (Management UI - guest/guest).

### 2. Running the Microservices
You can run each service in independent terminal windows by navigating to their respective directories or using the following commands from the root directory:

#### A. Order Service (Port: 5277)
```bash
dotnet run --project OrderService.Api/OrderService.Api.csproj
```
Swagger UI: [http://localhost:5277/swagger/index.html](http://localhost:5277/swagger/index.html)

#### B. Inventory Service (Port: 5050)
```bash
dotnet run --project InventoryService.Api/InventoryService.Api.csproj
```
Swagger UI: [http://localhost:5050/swagger/index.html](http://localhost:5050/swagger/index.html)

#### C. Payment Service (Port: 5032)
```bash
dotnet run --project PaymentService.Api/PaymentService.Api.csproj
```
Swagger UI: [http://localhost:5032/swagger/index.html](http://localhost:5032/swagger/index.html)

---

## 📝 Database and Migrations
When the services are run for the first time, they automatically create their databases (`ecommerce_order_db`, `ecommerce_inventory_db`, and `ecommerce_payment_db`) inside PostgreSQL and migrate their schemas.

If you want to apply migrations manually or add a new migration, you can use the following commands within the folder of the respective service:

```bash
# Example: Creating a new migration for the Order Service
cd OrderService.Api
dotnet ef migrations add <MigrationName> -o Infrastructure/Data/Migrations

# Updating the database
dotnet ef database update
```

---

## 🔒 Security and Configuration Note
To ensure the local development and evaluation process is **plug-and-play**, the PostgreSQL and RabbitMQ connection details for docker-compose are defined by default in the `appsettings.json` files. 

In real/production environments, it is recommended to store this sensitive data in files excluded from the repository (such as `appsettings.Development.json`), as **Environment Variables**, or in secure external secret stores like **Azure Key Vault** / **HashiCorp Vault**. The `.gitignore` file in this project is configured according to these standards.
