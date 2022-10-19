using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    public partial class Update01 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RELAY",
                table: "Addresses",
                newName: "UNCLAIMED_RAW");

            migrationBuilder.AddColumn<string>(
                name: "FEE_RAW",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GAS_LIMIT_RAW",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GAS_PRICE_RAW",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BURNED_SUPPLY_RAW",
                table: "Tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CURRENT_SUPPLY_RAW",
                table: "Tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MAX_SUPPLY_RAW",
                table: "Tokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MINTABLE",
                table: "Tokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VALUE_RAW",
                table: "TokenEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "INFUSED_VALUE_RAW",
                table: "InfusionEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FEE",
                table: "GasEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AMOUNT_RAW",
                table: "AddressStakes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UNCLAIMED_RAW",
                table: "AddressStakes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "STAKE_RAW",
                table: "Addresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AMOUNT_RAW",
                table: "AddressBalances",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FEE_RAW",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "GAS_LIMIT_RAW",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "GAS_PRICE_RAW",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "BURNED_SUPPLY_RAW",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "CURRENT_SUPPLY_RAW",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "MAX_SUPPLY_RAW",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "MINTABLE",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "VALUE_RAW",
                table: "TokenEvents");

            migrationBuilder.DropColumn(
                name: "INFUSED_VALUE_RAW",
                table: "InfusionEvents");

            migrationBuilder.DropColumn(
                name: "FEE",
                table: "GasEvents");

            migrationBuilder.DropColumn(
                name: "AMOUNT_RAW",
                table: "AddressStakes");

            migrationBuilder.DropColumn(
                name: "UNCLAIMED_RAW",
                table: "AddressStakes");

            migrationBuilder.DropColumn(
                name: "STAKE_RAW",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "AMOUNT_RAW",
                table: "AddressBalances");

            migrationBuilder.RenameColumn(
                name: "UNCLAIMED_RAW",
                table: "Addresses",
                newName: "RELAY");
        }
    }
}
