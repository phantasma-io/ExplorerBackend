using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update07 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddressStakes");

            migrationBuilder.RenameColumn(
                name: "UNCLAIMED_RAW",
                table: "Addresses",
                newName: "UNCLAIMED_AMOUNT_RAW");

            migrationBuilder.RenameColumn(
                name: "UNCLAIMED",
                table: "Addresses",
                newName: "UNCLAIMED_AMOUNT");

            migrationBuilder.RenameColumn(
                name: "STAKE_RAW",
                table: "Addresses",
                newName: "STAKED_AMOUNT_RAW");

            migrationBuilder.RenameColumn(
                name: "STAKE",
                table: "Addresses",
                newName: "STAKED_AMOUNT");

            migrationBuilder.AddColumn<long>(
                name: "STAKE_TIMESTAMP",
                table: "Addresses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "STAKE_TIMESTAMP",
                table: "Addresses");

            migrationBuilder.RenameColumn(
                name: "UNCLAIMED_AMOUNT_RAW",
                table: "Addresses",
                newName: "UNCLAIMED_RAW");

            migrationBuilder.RenameColumn(
                name: "UNCLAIMED_AMOUNT",
                table: "Addresses",
                newName: "UNCLAIMED");

            migrationBuilder.RenameColumn(
                name: "STAKED_AMOUNT_RAW",
                table: "Addresses",
                newName: "STAKE_RAW");

            migrationBuilder.RenameColumn(
                name: "STAKED_AMOUNT",
                table: "Addresses",
                newName: "STAKE");

            migrationBuilder.CreateTable(
                name: "AddressStakes",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    AMOUNT = table.Column<string>(type: "text", nullable: true),
                    AMOUNT_RAW = table.Column<string>(type: "text", nullable: true),
                    TIME = table.Column<long>(type: "bigint", nullable: false),
                    UNCLAIMED = table.Column<string>(type: "text", nullable: true),
                    UNCLAIMED_RAW = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressStakes", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AddressStakes_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddressStakes_AddressId",
                table: "AddressStakes",
                column: "AddressId",
                unique: true);
        }
    }
}
