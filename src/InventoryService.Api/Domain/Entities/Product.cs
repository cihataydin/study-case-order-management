namespace InventoryService.Api.Domain.Entities;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string Name { get; set; } = string.Empty;

    public int TotalStock { get; set; }
    
    public byte[] Version { get; set; } = null!;
}
