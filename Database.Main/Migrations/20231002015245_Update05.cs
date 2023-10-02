using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update05 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AddressBalances_Chains_ChainId",
                table: "AddressBalances");

            migrationBuilder.DropIndex(
                name: "IX_AddressBalances_ChainId",
                table: "AddressBalances");

            migrationBuilder.DropColumn(
                name: "ChainId",
                table: "AddressBalances");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChainId",
                table: "AddressBalances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AddressBalances_ChainId",
                table: "AddressBalances",
                column: "ChainId");

            migrationBuilder.AddForeignKey(
                name: "FK_AddressBalances_Chains_ChainId",
                table: "AddressBalances",
                column: "ChainId",
                principalTable: "Chains",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
