using System.Globalization;
using System.Numerics;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<BigInteger>(
                name: "TOTAL_SOUL_AMOUNT",
                table: "Addresses",
                type: "numeric",
                nullable: false,
                defaultValue: BigInteger.Parse("0", NumberFormatInfo.InvariantInfo));

            migrationBuilder.Sql("DELETE FROM \"GlobalVariables\" WHERE \"NAME\" = 'BALANCE_REFETCH_TIMESTAMP';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TOTAL_SOUL_AMOUNT",
                table: "Addresses");
        }
    }
}
