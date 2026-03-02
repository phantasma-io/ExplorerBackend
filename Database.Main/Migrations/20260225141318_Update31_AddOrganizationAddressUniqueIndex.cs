using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update31_AddOrganizationAddressUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the newest duplicate row so the unique index can be applied safely
            // on existing databases with historical duplicate inserts.
            migrationBuilder.Sql(
                """
                DELETE FROM "OrganizationAddresses" oa
                USING (
                    SELECT "ID"
                    FROM (
                        SELECT "ID",
                               ROW_NUMBER() OVER (PARTITION BY "OrganizationId", "AddressId" ORDER BY "ID" DESC) AS rn
                        FROM "OrganizationAddresses"
                    ) ranked
                    WHERE ranked.rn > 1
                ) duplicates
                WHERE oa."ID" = duplicates."ID";
                """);

            migrationBuilder.DropIndex(
                name: "IX_OrganizationAddresses_OrganizationId",
                table: "OrganizationAddresses");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAddresses_OrganizationId_AddressId",
                table: "OrganizationAddresses",
                columns: new[] { "OrganizationId", "AddressId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrganizationAddresses_OrganizationId_AddressId",
                table: "OrganizationAddresses");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAddresses_OrganizationId",
                table: "OrganizationAddresses",
                column: "OrganizationId");
        }
    }
}
