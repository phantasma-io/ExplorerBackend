using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MainDbContext))]
    [Migration("20251208090000_Update23")]
    public partial class Update23 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO ""EventKinds"" (""ID"", ""NAME"", ""ChainId"")
SELECT 69, 'SpecialResolution', ""ID""
FROM ""Chains""
WHERE NOT EXISTS (
    SELECT 1 FROM ""EventKinds"" WHERE ""NAME"" = 'SpecialResolution' AND ""ChainId"" = ""ID"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
