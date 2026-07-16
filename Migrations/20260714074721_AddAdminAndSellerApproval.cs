using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrendMarketServer.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAndSellerApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: true -> bu migration çalıştığında zaten var olan satıcı satırları
            // otomatik onaylı sayılır (geriye dönük uyum). C# tarafındaki model varsayılanı
            // (false) ise bundan sonra KOD üzerinden eklenen (yeni kayıt olan) satırlara uygulanır.
            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "Sellers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "Admins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admins", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Admins_Email",
                table: "Admins",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Admins");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "Sellers");
        }
    }
}
