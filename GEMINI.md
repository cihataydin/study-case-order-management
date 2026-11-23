# GEMINI.md - Study Case Tracker

Bu dosya, mülakat projesinin (Study Case) orijinal gereksinimlerini, şu ana kadar tamamlanan mimari kararları ve sonraki adımları özetleyen merkezi bir takip dosyasıdır. Yapay zeka asistanı (Gemini) ile yapılan geliştirmelerin context'ini (bağlamını) korumak için tasarlanmıştır.

## 📋 Orijinal Proje Özeti (Case Study - Raw)

**E-Commerce Order Management System - Technical Case**

**📋 Proje Özeti**
Bir e-ticaret platformu için sipariş yönetim mikro servisi geliştirilecektir. Sistem, sipariş oluşturma, stok kontrolü ve basit bir ödeme işlemi akışını yönetecektir.

**🎯 Kapsam / Geliştirme Gereksinimleri**

**1. Core Services (Zorunlu)**

*Order Service*
● Sipariş oluşturma ve durum yönetimi (PENDING → CONFIRMED → SHIPPED → DELIVERED)
● Sipariş iptali ve geri ödeme işlemleri
● Idempotency kontrolü (duplicate order prevention)
● Concurrent sipariş handling

*Inventory Service*
● Real-time stok kontrolü ve rezervasyonu
● Optimistic locking ile race condition prevention
● Stok rezervasyon timeout mekanizması (10 dakika)
● Bulk stock update desteği

*Payment Service (Mock)*
● Ödeme işlemi simülasyonu (%85 başarı, %10 timeout, %5 failure)
● Retry mechanism with exponential backoff
● Payment reversal için compensating transaction
● Fraud detection simulation (basit rule-based)

**2. Event-Driven Communication**
*Messaging:*
● RabbitMQ veya In-Memory Message Bus implementasyonu
● At-least-once delivery garantisi
● Dead Letter Queue handling

*Event Types:*
● OrderCreatedEvent
● StockReservedEvent
● StockReleasedEvent
● PaymentProcessedEvent
● PaymentFailedEvent
● OrderConfirmedEvent
● OrderCancelledEvent
● OrderShippedEvent

**API Specifications (REST Endpoints):**
*Order Management:*
`POST /api/v1/orders` # Create new order
`GET /api/v1/orders/{orderId}` # Get order details
`PUT /api/v1/orders/{orderId}/cancel` # Cancel order
`GET /api/v1/orders/customer/{customerId}` # List customer orders
`POST /api/v1/orders/{orderId}/retry` # Retry failed order

*Inventory Management:*
`POST /api/v1/inventory/check-availability` # Check multiple products
`POST /api/v1/inventory/reserve` # Reserve stock
`POST /api/v1/inventory/release` # Release reservation
`GET /api/v1/inventory/products/{productId}/stock` # Get current stock

*Payment Processing:*
`POST /api/v1/payments/process` # Process payment
`POST /api/v1/payments/refund` # Process refund
`GET /api/v1/payments/{paymentId}` # Get payment status
`POST /api/v1/payments/validate` # Validate payment method

**Business Rules & Constraints**
*Order Rules:*
● Minimum order amount: 100 TL
● Maximum order amount: 50,000 TL
● Maximum items per order: 20
● Order cancellation allowed within 2 hours
● VIP customers: Priority processing

*Inventory Rules:*
● Reserved stock auto-release after 10 minutes
● Low stock alert when quantity < 10
● Cannot reserve more than 50% of available stock per order
● Flash sale items: Max 2 per customer

*Payment Rules:*
● Payment retry max 3 attempts
● Refund processing time: 3-5 business days simulation
● Fraud detection: Orders > 10,000 TL require additional verification
● Multiple payment methods support (Credit Card, Wallet, Bank Transfer)

**Performance Requirements**
*SLA Metrics:*
● API Response Time: p95 < 200ms, p99 < 500ms
● Order Processing: < 3 seconds end-to-end
● Concurrent Orders: Support 1000 simultaneous orders
● Database Query Performance: All queries < 50ms

*Scalability:*
● Horizontal scaling ready
● Stateless service design
● Database connection pooling
● Caching strategy for frequently accessed data

**Error Handling & Monitoring**
*Error Scenarios:*
● Network failures
● Database connection issues
● Message broker downtime
● External service timeouts
● Concurrent modification conflicts
● Business rule violations

*Observability:*
● Structured logging (correlation ID tracking)
● Custom metrics (orders/min, success rate, revenue/hour)
● Health checks (liveness & readiness probes)
● Distributed tracing support
● Alert thresholds configuration

**🛠 Technology Stack**
*Required Technologies:*
● .NET 9
● Entity Framework Core 9 (Code First)
● PostgreSQL or MySQL
● MediatR (CQRS implementation)
● FluentValidation (input validation)
● Polly (resilience and transient fault handling)
● Serilog (structured logging)
● AutoMapper (object mapping)

*Testing Stack:*
● xUnit (unit & integration tests)
● Moq or NSubstitute (mocking)
● FluentAssertions (test assertions)
● TestContainers (integration testing)
● Bogus (test data generation)

*Optional (Bonus Points):*
● RabbitMQ or Azure Service Bus
● Redis (distributed caching)
● Elasticsearch (log aggregation)
● OpenTelemetry (distributed tracing)
● GraphQL (alternative API)
● gRPC (inter-service communication)

**📦 Deliverables (3 Days)**
*Day 1: Design & Setup*
● System design documentation
● API contracts (OpenAPI/Swagger)
● Database design decisions
● Development environment setup
● Basic project structure

*Day 2: Core Implementation*
● Order service implementation
● Inventory service implementation
● Payment service (mock)
● Saga orchestration
● Basic error handling

*Day 3: Testing & Documentation*
● Unit tests (minimum 70% coverage)
● Integration tests for critical paths
● Docker compose setup
● README with setup instructions
● Postman/Insomnia collection

---

## ✅ Tamamlananlar (Day 1: Setup & Design)

*   **Mimari Kurulum:** Aynı Solution içerisinde 3 farklı Web API (`OrderService.Api`, `InventoryService.Api`, `PaymentService.Api`) ve 1 Ortak Kütüphane (`Shared`) oluşturuldu.
*   **Altyapı (Docker):** `docker-compose.yml` ile PostgreSQL (5432) ve RabbitMQ (5672/15672) container altyapısı kuruldu.
*   **Veritabanı (EF Core 9):**
    *   `ecommerce_order_db`: `Orders` (`IdempotencyKey` unique index eklendi) ve `OrderItems` tabloları.
    *   `ecommerce_inventory_db`: `Products` (`RowVersion` ile Optimistic Locking sağlandı) ve `StockReservations` (`ExpiresAt` ile timeout altyapısı eklendi) tabloları.
    *   `ecommerce_payment_db`: `Payments` tablosu.
    *   *Migrations:* Tüm DbContext'ler için `Infrastructure/Data/Migrations` klasörüne temiz mimariyle aktarıldı. Servis ayağa kalktığında `dbContext.Database.Migrate()` ile otomatik migrate (Auto-Migrate) çalışması sağlandı.
*   **API & Test Araçları:**
    *   Tüm controller'lar için Swagger arayüzü kuruldu ve `501 Not Implemented` dönen tüm case-study endpoint taslakları eklendi.
    *   Geliştirme aşamasında tarayıcı veya Postman kullanmadan test edebilmek için VS Code/Rider `.http` dosyaları güncel endpointlere göre tasarlandı.
*   **Loglama:** Serilog entegre edildi ve konsola (Console) formatlı yazdırıldı.
*   **Bağımlılıklar (Packages):** 
    *   **MassTransit:** v9.x sürümlerindeki ticari lisans gereksinimi/hatasını aşmak ve projenin tak-çalıştır olmasını sağlamak için açık kaynak `8.3.4` sürümü kullanıldı.
    *   MediatR, FluentValidation, Npgsql.EntityFrameworkCore.PostgreSQL paketleri entegre edildi.
*   **Dokümantasyon & Temizlik:** 
    *   `README.md` ve `system_design.md` İngilizce global standartlara göre oluşturuldu.
    *   Özel bilgiler ve loglar için .NET standartlarına uygun `.gitignore` dosyası eklendi, gereksiz bağlantılar kısıtlandı.
    *   Tüm servislerin `Program.cs` dosyalarındaki gereksiz yorum satırları ve ölü kodlar temizlenerek "Clean Code" standartlarına getirildi.

---

## ✅ Tamamlananlar (Day 2: Core Implementation)

1.  **Event Tanımlamaları (`Shared` Projesi):**
    *   Mikroservislerin birbiriyle konuşacağı Record event sınıfları (`OrderCreatedEvent`, `StockReservedEvent`, `PaymentProcessedEvent`, vb.) yazıldı ve `TotalAmount` gibi gerekli verilerle zenginleştirildi.
2.  **Order Service İş Mantığı (CQRS / MediatR):**
    *   `CreateOrderCommand`, `Handler` ve `Validator` yazıldı.
    *   `IdempotencyKey` üzerinden DB kontrolü (duplicate order prevention) eklendi.
    *   Sipariş `PENDING` statüsünde kaydedilip `OrderCreatedEvent` publish edildi.
3.  **Inventory Service İş Mantığı:**
    *   `OrderCreatedEventConsumer` yazıldı. Stok kontrolü yapıldı (50% rule) ve ürün stok adedi düşürülerek `StockReservation` tablosuna 10 dk geçerli kayıt atıldı.
    *   Başarılı/Başarısız duruma göre `StockReservedEvent` veya `StockReleasedEvent` fırlatıldı.
4.  **Payment Service İş Mantığı:**
    *   `StockReservedEventConsumer` yazıldı.
    *   Polly kütüphanesi ile Retry mekanizmaları (Exponential Backoff) eklendi.
    *   %85 başarı, %10 timeout, %5 hata ve 10,000 TL üzeri Fraud simülasyonu entegre edildi.
5.  **Saga / Compensating Transactions:**
    *   Order Service içinde Payment ve Stock sonuçlarını dinleyen Consumer'lar yazıldı. Sipariş statüleri `CONFIRMED` veya `CANCELLED` olarak güncellendi.
    *   Inventory Service içine `OrderCancelledEventConsumer` eklenerek, başarısız durumlarda rezerve edilen stokların serbest bırakılması (release) sağlandı.
6.  **Observability & Gözlemlenebilirlik (OpenTelemetry & Metrics):**
    *   Tüm servislere `OpenTelemetry` entegrasyonu sağlandı (Tracing & OTLP Jaeger exporter).
    *   Dağıtık izleme için `docker-compose.yml` içerisine `jaeger` eklendi.
    *   `OrderMetrics` adında özel bir `System.Diagnostics.Metrics.Meter` tasarlandı (`orders.created.count`, `orders.success.count`, vb.).
    *   Prometheus endpoint'i `/metrics` tüm API'ler için dışa açıldı.
7.  **Global Error Handling & Resilience:**
    *   `Shared` projesinde `IExceptionHandler` arayüzü ile standart RFC-7807 (ProblemDetails) dönen `GlobalExceptionHandler` yazıldı ve tüm servislere entegre edildi.
    *   `MassTransit` bağlantılarında `.UseMessageRetry()` ile gecikmeli tekrar deneme (Exponential Backoff / Retry Policy) mekanizması eklendi.

---

---

## ✅ Tamamlananlar (Day 3: Testing & Documentation)

1.  **Unit Tests (59 Unit Tests):**
    *   `OrderService.Api.UnitTests`: Command/query handlers, business validators, and event consumers are 100% tested under EF Core InMemory provider.
    *   `InventoryService.Api.UnitTests`: Tested all queries/commands, low-stock warnings, 50% reservation limit constraints, and VIP priority checks.
    *   `PaymentService.Api.UnitTests`: Tested refund delay simulation, payment method validation (16 digits), and payment consumers with Polly retry and fraud detection rules.
    *   *Resilience mocking:* Successfully mocked Random and Polly retry behaviors using custom subclasses and reflection, making the test suite execute instantly without thread blocking.
2.  **Integration Tests (Testcontainers):**
    *   `OrderService.Api.IntegrationTests`: Configured with `Testcontainers.PostgreSql`. Automatically spins up a real PostgreSQL container, applies migrations, runs CRUD operations, and asserts database constraints (like unique indexes on `IdempotencyKey`).
3.  **Documentation & API Tools:**
    *   `README.md` updated with comprehensive instructions on running unit tests and Testcontainers integration tests.
    *   `ECommerce_Order_Management.postman_collection.json` created in the root directory for easy import into Postman/Insomnia.
    *   VS Code `.http` files updated for local endpoint testing.

