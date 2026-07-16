using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrendMarketServer.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantSupportToCartAndOrderItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorVariantLabel",
                table: "OrderItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SizeVariantLabel",
                table: "OrderItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ColorVariantId",
                table: "CartEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SizeVariantId",
                table: "CartEntries",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorVariantLabel",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SizeVariantLabel",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ColorVariantId",
                table: "CartEntries");

            migrationBuilder.DropColumn(
                name: "SizeVariantId",
                table: "CartEntries");
        }
    }
}
