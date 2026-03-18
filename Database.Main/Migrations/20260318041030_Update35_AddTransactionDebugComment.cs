using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update35_AddTransactionDebugComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DEBUG_COMMENT",
                table: "Transactions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DEBUG_COMMENT",
                table: "Transactions");
        }
    }
}
