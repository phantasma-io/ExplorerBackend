using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_ChainId",
                table: "Contracts");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ChainId_HASH",
                table: "Contracts",
                columns: new[] { "ChainId", "HASH" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contracts_ChainId_HASH",
                table: "Contracts");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ChainId",
                table: "Contracts",
                column: "ChainId");
        }
    }
}
