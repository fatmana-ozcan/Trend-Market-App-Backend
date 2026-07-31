using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrendMarketServer.Migrations
{
    /// <inheritdoc />
    public partial class ScopeCartFavoritesAndViewsToCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sepet, favoriler ve gezinti geçmişi artık cihaz oturumuna değil hesaba bağlanabiliyor.
            // Mevcut sepet/favori satırları giriş yapılmadan oluşturulmuş sayılır (CustomerId = 0),
            // ilk girişte AdoptSessionData bunları hesaba devreder. Mevcut ProductViews satırları
            // ise zaten bir hesaba aitti; SessionId = "" varsayılanı onları doğru tarafta bırakır.
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "CartEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "FavoriteEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "ProductViews",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Tekillik artık sahiplik anahtarını (CustomerId + SessionId) da içeriyor; aksi halde
            // aynı cihazda A hesabının bıraktığı satır, B hesabının aynı ürünü eklemesini engellerdi.
            migrationBuilder.DropIndex(
                name: "IX_CartEntries_SessionId_ProductId_ColorVariantId_SizeVariantId",
                table: "CartEntries");

            migrationBuilder.CreateIndex(
                name: "IX_CartEntries_CustomerId_SessionId_ProductId_ColorVariantId_SizeVariantId",
                table: "CartEntries",
                columns: new[] { "CustomerId", "SessionId", "ProductId", "ColorVariantId", "SizeVariantId" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_FavoriteEntries_SessionId_ProductId",
                table: "FavoriteEntries");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteEntries_CustomerId_SessionId_ProductId",
                table: "FavoriteEntries",
                columns: new[] { "CustomerId", "SessionId", "ProductId" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_ProductViews_CustomerId_ProductId",
                table: "ProductViews");

            migrationBuilder.CreateIndex(
                name: "IX_ProductViews_CustomerId_SessionId_ProductId",
                table: "ProductViews",
                columns: new[] { "CustomerId", "SessionId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductViews_CustomerId_SessionId_ProductId",
                table: "ProductViews");

            migrationBuilder.CreateIndex(
                name: "IX_ProductViews_CustomerId_ProductId",
                table: "ProductViews",
                columns: new[] { "CustomerId", "ProductId" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_FavoriteEntries_CustomerId_SessionId_ProductId",
                table: "FavoriteEntries");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteEntries_SessionId_ProductId",
                table: "FavoriteEntries",
                columns: new[] { "SessionId", "ProductId" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_CartEntries_CustomerId_SessionId_ProductId_ColorVariantId_SizeVariantId",
                table: "CartEntries");

            migrationBuilder.CreateIndex(
                name: "IX_CartEntries_SessionId_ProductId_ColorVariantId_SizeVariantId",
                table: "CartEntries",
                columns: new[] { "SessionId", "ProductId", "ColorVariantId", "SizeVariantId" },
                unique: true);

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "ProductViews");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "FavoriteEntries");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "CartEntries");
        }
    }
}
