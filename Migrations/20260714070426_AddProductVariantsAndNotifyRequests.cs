using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrendMarketServer.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariantsAndNotifyRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductNotifyRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    VariantId = table.Column<int>(type: "INTEGER", nullable: true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductNotifyRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductVariants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: true),
                    Size = table.Column<string>(type: "TEXT", nullable: true),
                    Image = table.Column<string>(type: "TEXT", nullable: true),
                    Stock = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVariants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductNotifyRequests_CustomerId_ProductId_VariantId",
                table: "ProductNotifyRequests",
                columns: new[] { "CustomerId", "ProductId", "VariantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductNotifyRequests");

            migrationBuilder.DropTable(
                name: "ProductVariants");
        }
    }
}
