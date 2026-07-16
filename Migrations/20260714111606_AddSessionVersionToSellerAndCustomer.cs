using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrendMarketServer.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionVersionToSellerAndCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "Sellers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionVersion",
                table: "Sellers");

            migrationBuilder.DropColumn(
                name: "SessionVersion",
                table: "Customers");
        }
    }
}
