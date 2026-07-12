using InventoryService.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Api.Infrastructure.Data;

public static class InventoryDbSeeder
{
    public static async Task SeedAsync(InventoryDbContext context)
    {
        if (await context.Products.AnyAsync())
        {
            return;
        }

        var products = new List<Product>();
        var random = new Random(42);

        for (int i = 1; i <= 50; i++)
        {
            var guid = Guid.Parse($"11111111-1111-1111-1111-{i.ToString("D12")}");
            var product = new Product
            {
                Id = guid,
                Name = $"Product {i}",
                Price = Math.Round((decimal)(random.NextDouble() * 4950 + 50), 2) // 50 to 5000
            };
            product.IncreaseStock(random.Next(10, 1000));
            
            // Insert via Raw SQL to bypass EF Core ignoring IsRowVersion fields on Insert
            var versionBytes = Guid.NewGuid().ToByteArray();
            var sql = @"INSERT INTO ""Products"" (""Id"", ""Name"", ""Price"", ""TotalStock"", ""Version"") VALUES (@p0, @p1, @p2, @p3, @p4)";
            await context.Database.ExecuteSqlRawAsync(sql, product.Id, product.Name, product.Price, product.TotalStock, versionBytes);
        }
    }
}
