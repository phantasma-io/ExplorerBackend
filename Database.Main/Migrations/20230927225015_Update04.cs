using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update04 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PRICE_AUD",
                table: "TokenDailyPrices");

            migrationBuilder.DropColumn(
                name: "PRICE_CAD",
                table: "TokenDailyPrices");

            migrationBuilder.DropColumn(
                name: "PRICE_CNY",
                table: "TokenDailyPrices");

            migrationBuilder.DropColumn(
                name: "PRICE_ETH",
                table: "TokenDailyPrices");

            migrationBuilder.DropColumn(
                name: "PRICE_EUR",
                table: "TokenDailyPrices");

            migrationBuilder.DropColumn(
                name: "PRICE_GBP",
                table: "TokenDailyPrices");

            migrationBuilder.DropColumn(
                name: "PRICE_JPY",
                table: "TokenDailyPrices");

            migrationBuilder.DropColumn(
                name: "PRICE_NEO",
                table: "TokenDailyPrices");

            migrationBuilder.DropColumn(
                name: "PRICE_RUB",
                table: "TokenDailyPrices");

            migrationBuilder.DropColumn(
                name: "PRICE_SOUL",
                table: "TokenDailyPrices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PRICE_AUD",
                table: "TokenDailyPrices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PRICE_CAD",
                table: "TokenDailyPrices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PRICE_CNY",
                table: "TokenDailyPrices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PRICE_ETH",
                table: "TokenDailyPrices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PRICE_EUR",
                table: "TokenDailyPrices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PRICE_GBP",
                table: "TokenDailyPrices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PRICE_JPY",
                table: "TokenDailyPrices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PRICE_NEO",
                table: "TokenDailyPrices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PRICE_RUB",
                table: "TokenDailyPrices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PRICE_SOUL",
                table: "TokenDailyPrices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
