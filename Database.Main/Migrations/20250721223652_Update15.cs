using System.Globalization;
using System.Numerics;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update15 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /*migrationBuilder.AlterColumn<BigInteger>(
                name: "AMOUNT_RAW",
                table: "AddressBalances",
                type: "numeric",
                nullable: false,
                defaultValue: BigInteger.Parse("0", NumberFormatInfo.InvariantInfo),
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);*/
            
            migrationBuilder.Sql("""
                ALTER TABLE "AddressBalances"
                ALTER COLUMN "AMOUNT_RAW" TYPE numeric USING "AMOUNT_RAW"::numeric;
            """);

            migrationBuilder.Sql("""
                UPDATE "AddressBalances" SET "AMOUNT_RAW" = 0 WHERE "AMOUNT_RAW" IS NULL;
            """);

            migrationBuilder.AlterColumn<decimal>(
                name: "AMOUNT_RAW",
                table: "AddressBalances",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "AMOUNT_RAW",
                table: "AddressBalances",
                type: "text",
                nullable: true,
                oldClrType: typeof(BigInteger),
                oldType: "numeric");
        }
    }
}
