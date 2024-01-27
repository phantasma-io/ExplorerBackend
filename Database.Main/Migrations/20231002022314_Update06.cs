using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update06 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddressEvents");

            migrationBuilder.AddColumn<int>(
                name: "TargetAddressId",
                table: "Events",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_TargetAddressId",
                table: "Events",
                column: "TargetAddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Addresses_TargetAddressId",
                table: "Events",
                column: "TargetAddressId",
                principalTable: "Addresses",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Addresses_TargetAddressId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_TargetAddressId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TargetAddressId",
                table: "Events");

            migrationBuilder.CreateTable(
                name: "AddressEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressEvents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AddressEvents_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AddressEvents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddressEvents_AddressId",
                table: "AddressEvents",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressEvents_EventId",
                table: "AddressEvents",
                column: "EventId",
                unique: true);
        }
    }
}
