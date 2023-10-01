using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.ApiCache.Migrations
{
    /// <inheritdoc />
    public partial class Update01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CURRENT_HEIGHT",
                table: "Chains");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CURRENT_HEIGHT",
                table: "Chains",
                type: "text",
                nullable: true);
        }
    }
}
