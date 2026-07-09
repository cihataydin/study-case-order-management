namespace InventoryService.Api.Domain.Entities;

public class StockReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }
    
    public DateTime ExpiresAt { get; set; }
}
