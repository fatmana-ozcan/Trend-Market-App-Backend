using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrendMarketServer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCartEntryUniqueIndexForVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CartEntries_SessionId_ProductId",
                table: "CartEntries");

            migrationBuilder.CreateIndex(
                name: "IX_CartEntries_SessionId_ProductId_ColorVariantId_SizeVariantId",
                table: "CartEntries",
                columns: new[] { "SessionId", "ProductId", "ColorVariantId", "SizeVariantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CartEntries_SessionId_ProductId_ColorVariantId_SizeVariantId",
                table: "CartEntries");

            migrationBuilder.CreateIndex(
                name: "IX_CartEntries_SessionId_ProductId",
                table: "CartEntries",
                columns: new[] { "SessionId", "ProductId" },
                unique: true);
        }
    }
}
