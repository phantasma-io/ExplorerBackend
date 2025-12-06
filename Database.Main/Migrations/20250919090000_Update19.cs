using Database.Main;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MainDbContext))]
    [Migration("20250919090000_Update19")]
    public partial class Update19 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS ""pg_trgm"";");

            migrationBuilder.Sql(
                @"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_Search_Transactions_Hash_trgm"" ON ""Transactions"" USING gin (""HASH"" gin_trgm_ops);",
                suppressTransaction: true);

            migrationBuilder.Sql(
                @"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_Search_Blocks_Hash_trgm"" ON ""Blocks"" USING gin (""HASH"" gin_trgm_ops);",
                suppressTransaction: true);

            migrationBuilder.Sql(
                @"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_Search_Blocks_Height"" ON ""Blocks"" (""HEIGHT"");",
                suppressTransaction: true);

            migrationBuilder.Sql(
                @"CREATE INDEX CONCURRENTLY IF NOT EXISTS ""IX_Search_Events_TransactionId"" ON ""Events"" (""TransactionId"");",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP INDEX CONCURRENTLY IF EXISTS ""IX_Search_Transactions_Hash_trgm"";",
                suppressTransaction: true);

            migrationBuilder.Sql(
                @"DROP INDEX CONCURRENTLY IF EXISTS ""IX_Search_Blocks_Hash_trgm"";",
                suppressTransaction: true);

            migrationBuilder.Sql(
                @"DROP INDEX CONCURRENTLY IF EXISTS ""IX_Search_Blocks_Height"";",
                suppressTransaction: true);

            migrationBuilder.Sql(
                @"DROP INDEX CONCURRENTLY IF EXISTS ""IX_Search_Events_TransactionId"";",
                suppressTransaction: true);
        }
    }
}
