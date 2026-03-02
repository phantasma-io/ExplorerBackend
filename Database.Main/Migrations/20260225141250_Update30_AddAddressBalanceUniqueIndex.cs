using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update30_AddAddressBalanceUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the newest duplicate row so the unique index can be applied safely
            // on existing databases with historical duplicate inserts.
            migrationBuilder.Sql(
                """
                DELETE FROM "AddressBalances" ab
                USING (
                    SELECT "ID"
                    FROM (
                        SELECT "ID",
                               ROW_NUMBER() OVER (PARTITION BY "AddressId", "TokenId" ORDER BY "ID" DESC) AS rn
                        FROM "AddressBalances"
                    ) ranked
                    WHERE ranked.rn > 1
                ) duplicates
                WHERE ab."ID" = duplicates."ID";
                """);

            migrationBuilder.DropIndex(
                name: "IX_AddressBalances_AddressId",
                table: "AddressBalances");

            migrationBuilder.CreateIndex(
                name: "IX_AddressBalances_AddressId_TokenId",
                table: "AddressBalances",
                columns: new[] { "AddressId", "TokenId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AddressBalances_AddressId_TokenId",
                table: "AddressBalances");

            migrationBuilder.CreateIndex(
                name: "IX_AddressBalances_AddressId",
                table: "AddressBalances",
                column: "AddressId");
        }
    }
}
