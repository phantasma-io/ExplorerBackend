using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update29_AddSeriesCreatedUnixSeconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SERIES_CREATED_UNIX_SECONDS",
                table: "Serieses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_SERIES_CREATED_UNIX_SECONDS_ID",
                table: "Serieses",
                columns: new[] { "SERIES_CREATED_UNIX_SECONDS", "ID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Serieses_SERIES_CREATED_UNIX_SECONDS_ID",
                table: "Serieses");

            migrationBuilder.DropColumn(
                name: "SERIES_CREATED_UNIX_SECONDS",
                table: "Serieses");
        }
    }
}
