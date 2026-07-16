using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrendMarketServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceToProductVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "ProductVariants",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "ProductVariants");
        }
    }
}
