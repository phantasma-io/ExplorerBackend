using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update21 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Events_EventKindId""
                ON ""Events"" (""EventKindId"");
            ");

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_Events_EventKind_Chain_Timestamp_Id""
                ON ""Events"" (""EventKindId"", ""ChainId"", ""TIMESTAMP_UNIX_SECONDS"" DESC, ""ID"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_Events_EventKind_Chain_Timestamp_Id"";
            ");

            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_Events_EventKindId"";
            ");
        }
    }
}
