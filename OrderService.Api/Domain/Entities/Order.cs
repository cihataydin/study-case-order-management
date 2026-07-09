using OrderService.Api.Domain.Enums;

namespace OrderService.Api.Domain.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
    
    public string IdempotencyKey { get; set; } = string.Empty;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
