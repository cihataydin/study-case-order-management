using System;
using System.Collections.Generic;

namespace Shared.Events;

public record OrderItemDto(Guid ProductId, int Quantity, decimal UnitPrice);

public record OrderCreatedEvent(Guid OrderId, Guid CustomerId, decimal TotalAmount, List<OrderItemDto> Items, bool IsVip, string PaymentMethod);

public record StockReservedEvent(Guid OrderId, decimal TotalAmount, string PaymentMethod, bool IsVip);

public record StockReleasedEvent(Guid OrderId, string Reason, bool IsVip = false);

public record PaymentProcessedEvent(Guid OrderId, Guid PaymentId, bool IsVip);

public record PaymentFailedEvent(Guid OrderId, string Reason, bool IsVip);

public record OrderConfirmedEvent(Guid OrderId, bool IsVip = false);

public record OrderCancelledEvent(Guid OrderId, string Reason, List<OrderItemDto>? Items = null, bool IsVip = false);

public record OrderShippedEvent(Guid OrderId);

public record OrderDeliveredEvent(Guid OrderId);
