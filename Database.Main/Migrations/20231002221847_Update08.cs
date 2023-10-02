using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update08 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddressStorages");

            migrationBuilder.AddColumn<string>(
                name: "AVATAR",
                table: "Addresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "STORAGE_AVAILABLE",
                table: "Addresses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "STORAGE_USED",
                table: "Addresses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AVATAR",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "STORAGE_AVAILABLE",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "STORAGE_USED",
                table: "Addresses");

            migrationBuilder.CreateTable(
                name: "AddressStorages",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    AVAILABLE = table.Column<long>(type: "bigint", nullable: false),
                    AVATAR = table.Column<string>(type: "text", nullable: true),
                    USED = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressStorages", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AddressStorages_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddressStorages_AddressId",
                table: "AddressStorages",
                column: "AddressId",
                unique: true);
        }
    }
}
