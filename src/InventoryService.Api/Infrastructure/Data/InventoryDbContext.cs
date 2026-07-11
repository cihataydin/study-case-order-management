using Microsoft.EntityFrameworkCore;
using InventoryService.Api.Domain.Entities;

namespace InventoryService.Api.Infrastructure.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; } = null!;   
    public DbSet<StockReservation> StockReservations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Version).IsRowVersion();
        });

        modelBuilder.Entity<StockReservation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrderId, e.ProductId }).IsUnique();
        });
        
        base.OnModelCreating(modelBuilder);
    }
}
