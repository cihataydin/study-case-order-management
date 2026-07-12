using System;
using System.Collections.Generic;
using MediatR;
using Shared.Enums;

namespace OrderService.Api.Application.Orders.Commands;

public record CreateOrderItemDto(Guid ProductId, int Quantity);

public record CreateOrderRequest(
    Guid CustomerId, 
    List<CreateOrderItemDto> Items,
    bool IsVip,
    PaymentMethod PaymentMethod);

public record CreateOrderCommand(
    Guid CustomerId, 
    string IdempotencyKey, 
    List<CreateOrderItemDto> Items,
    bool IsVip,
    PaymentMethod PaymentMethod) : IRequest<Guid>;

