using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update02 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Addresses_Organizations_OrganizationId",
                table: "Addresses");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_OrganizationId",
                table: "Addresses");

            migrationBuilder.AddColumn<string>(
                name: "ADDRESS",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ADDRESS_NAME",
                table: "Organizations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AddressOrganization",
                columns: table => new
                {
                    AddressesID = table.Column<int>(type: "integer", nullable: false),
                    OrganizationsID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressOrganization", x => new { x.AddressesID, x.OrganizationsID });
                    table.ForeignKey(
                        name: "FK_AddressOrganization_Addresses_AddressesID",
                        column: x => x.AddressesID,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AddressOrganization_Organizations_OrganizationsID",
                        column: x => x.OrganizationsID,
                        principalTable: "Organizations",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddressOrganization_OrganizationsID",
                table: "AddressOrganization",
                column: "OrganizationsID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddressOrganization");

            migrationBuilder.DropColumn(
                name: "ADDRESS",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ADDRESS_NAME",
                table: "Organizations");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_OrganizationId",
                table: "Addresses",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_Organizations_OrganizationId",
                table: "Addresses",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "ID");
        }
    }
}
