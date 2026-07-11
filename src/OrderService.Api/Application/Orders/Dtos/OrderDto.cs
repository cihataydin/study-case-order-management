using System;
using System.Collections.Generic;
using OrderService.Api.Domain.Enums;

namespace OrderService.Api.Application.Orders.Dtos;

public class OrderDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public bool IsVip { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();
}
