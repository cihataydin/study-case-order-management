using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentService.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangePaymentMethodToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Method",
                table: "Payments");

            migrationBuilder.AddColumn<int>(
                name: "Method",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Method",
                table: "Payments",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
