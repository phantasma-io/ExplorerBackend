using Database.Main;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MainDbContext))]
    [Migration("20250923090000_Update20")]
    public partial class Update20 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Tokens"" ADD COLUMN IF NOT EXISTS ""CARBON_TOKEN_SCHEMAS"" bytea;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Tokens"" DROP COLUMN IF EXISTS ""CARBON_TOKEN_SCHEMAS"";");
        }
    }
}
