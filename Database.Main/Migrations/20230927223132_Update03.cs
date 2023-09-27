using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update03 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokenPriceStates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TokenPriceStates",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TokenId = table.Column<int>(type: "integer", nullable: false),
                    COIN_GECKO = table.Column<bool>(type: "boolean", nullable: false),
                    LAST_CHECK_DATE_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenPriceStates", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TokenPriceStates_Tokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "Tokens",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokenPriceStates_LAST_CHECK_DATE_UNIX_SECONDS",
                table: "TokenPriceStates",
                column: "LAST_CHECK_DATE_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_TokenPriceStates_TokenId",
                table: "TokenPriceStates",
                column: "TokenId",
                unique: true);
        }
    }
}
