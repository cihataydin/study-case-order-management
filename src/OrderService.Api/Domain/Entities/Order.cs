using OrderService.Api.Domain.Enums;
using Shared.Enums;

namespace OrderService.Api.Domain.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; private set; }
    
    public string IdempotencyKey { get; set; } = string.Empty;

    public bool IsVip { get; set; }
    
    public PaymentMethod PaymentMethod { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public static Order Create(Guid customerId, string idempotencyKey, bool isVip, PaymentMethod paymentMethod, List<OrderItem> items)
    {
        if (items == null || items.Count == 0)
            throw new InvalidOperationException("Order must contain at least one item.");

        if (items.Count > 20)
            throw new InvalidOperationException("Maximum items per order is 20.");

        var totalAmount = items.Sum(i => i.Quantity * i.UnitPrice);

        if (totalAmount < 100)
            throw new InvalidOperationException("Minimum order amount is 100 TL.");

        if (totalAmount > 50000)
            throw new InvalidOperationException("Maximum order amount is 50,000 TL.");

        return new Order
        {
            CustomerId = customerId,
            IdempotencyKey = idempotencyKey,
            TotalAmount = totalAmount,
            IsVip = isVip,
            PaymentMethod = paymentMethod,
            Items = items
        };
    }

    public void Cancel()
    {
        if ((DateTime.UtcNow - CreatedAt).TotalHours > 2)
            throw new InvalidOperationException("Order cannot be cancelled after 2 hours.");

        if (Status == OrderStatus.Cancelled || Status == OrderStatus.Delivered || Status == OrderStatus.Shipped)
            throw new InvalidOperationException($"Order cannot be cancelled because it is in {Status} status.");

        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Ship()
    {
        if (Status != OrderStatus.Confirmed)
            throw new InvalidOperationException("Order must be in Confirmed status to be shipped.");

        Status = OrderStatus.Shipped;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deliver()
    {
        if (Status != OrderStatus.Shipped)
            throw new InvalidOperationException("Order must be in Shipped status to be delivered.");

        Status = OrderStatus.Delivered;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Order must be in Pending status to be confirmed.");

        Status = OrderStatus.Confirmed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed()
    {
        Status = OrderStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Retry()
    {
        if (Status != OrderStatus.Failed)
            throw new InvalidOperationException("Only failed orders can be retried.");

        Status = OrderStatus.Pending;
        UpdatedAt = DateTime.UtcNow;
    }
}
