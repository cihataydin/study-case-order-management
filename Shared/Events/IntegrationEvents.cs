using System;
using System.Collections.Generic;

namespace Shared.Events;

public record OrderItemDto(Guid ProductId, int Quantity, decimal UnitPrice);

public record OrderCreatedEvent(Guid OrderId, Guid CustomerId, decimal TotalAmount, List<OrderItemDto> Items, bool IsVip, string PaymentMethod);

public record StockReservedEvent(Guid OrderId, decimal TotalAmount, string PaymentMethod);

public record StockReleasedEvent(Guid OrderId, string Reason);

public record PaymentProcessedEvent(Guid OrderId, Guid PaymentId);

public record PaymentFailedEvent(Guid OrderId, string Reason);

public record OrderConfirmedEvent(Guid OrderId);

public record OrderCancelledEvent(Guid OrderId, string Reason);

public record OrderShippedEvent(Guid OrderId);
