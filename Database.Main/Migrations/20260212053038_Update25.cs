using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update25 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TIMESTAMP_UNIX_SECONDS_ID",
                table: "Transactions",
                columns: new[] { "TIMESTAMP_UNIX_SECONDS", "ID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_TIMESTAMP_UNIX_SECONDS_ID",
                table: "Transactions");
        }
    }
}
