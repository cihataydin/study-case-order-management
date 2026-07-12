namespace InventoryService.Api.Domain.Entities;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string Name { get; set; } = string.Empty;

    public int TotalStock { get; private set; }
    
    public decimal Price { get; set; }
    
    public byte[] Version { get; set; } = null!;

    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));
        if (TotalStock < quantity) throw new InvalidOperationException($"Insufficient stock for product {Id}. Available: {TotalStock}, Requested: {quantity}");
        
        TotalStock -= quantity;
    }

    public void IncreaseStock(int quantity)
    {
        if (quantity < 0) throw new ArgumentException("Quantity cannot be negative.", nameof(quantity));
        TotalStock += quantity;
    }
}
