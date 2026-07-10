using System;
using System.Collections.Generic;
using MediatR;

namespace OrderService.Api.Application.Orders.Commands;

public record CreateOrderItemDto(Guid ProductId, int Quantity, decimal UnitPrice);

public record CreateOrderCommand(
    Guid CustomerId, 
    string IdempotencyKey, 
    List<CreateOrderItemDto> Items,
    bool IsVip,
    string PaymentMethod) : IRequest<Guid>;
