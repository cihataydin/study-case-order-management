using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryService.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedSampleProductsRawSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sql = new System.Text.StringBuilder();
            var random = new System.Random(42);
            for (int i = 1; i <= 15; i++)
            {
                var guid = $"11111111-1111-1111-1111-{i.ToString("D12")}";
                var stock = random.Next(10, 1000);
                var price = random.Next(50, 5000);
                sql.AppendLine($"INSERT INTO \"Products\" (\"Id\", \"Name\", \"TotalStock\", \"Price\", \"Version\") VALUES ('{guid}', 'Product {i}', {stock}, {price}, decode('0000000000000001', 'hex'));");
            }
            migrationBuilder.Sql(sql.ToString());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"Products\" WHERE \"Id\" IN (SELECT \"Id\" FROM \"Products\" WHERE \"Id\"::text LIKE '11111111-1111-1111-1111-%');");
        }
    }
}
