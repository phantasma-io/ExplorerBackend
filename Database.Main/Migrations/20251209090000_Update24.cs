using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MainDbContext))]
    [Migration("20251209090000_Update24")]
    public partial class Update24 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BALANCE_DIRTY_BLOCK",
                table: "Addresses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_ChainId_BALANCE_DIRTY_BLOCK",
                table: "Addresses",
                columns: new[] { "ChainId", "BALANCE_DIRTY_BLOCK" });

            migrationBuilder.Sql(@"
UPDATE ""Addresses"" a
SET ""BALANCE_DIRTY_BLOCK"" = CAST(c.""CURRENT_HEIGHT"" AS BIGINT)
FROM ""Chains"" c
WHERE a.""ChainId"" = c.""ID"" AND a.""ADDRESS"" <> 'NULL';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Addresses_ChainId_BALANCE_DIRTY_BLOCK",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "BALANCE_DIRTY_BLOCK",
                table: "Addresses");
        }
    }
}
