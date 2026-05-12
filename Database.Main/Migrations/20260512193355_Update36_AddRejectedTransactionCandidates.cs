using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update36_AddRejectedTransactionCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RejectedTransactionCandidates",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HASH = table.Column<string>(type: "text", nullable: false),
                    NEXUS = table.Column<string>(type: "text", nullable: false),
                    CHAIN = table.Column<string>(type: "text", nullable: false),
                    BLOCK_HEIGHT = table.Column<long>(type: "bigint", nullable: true),
                    BLOCK_HASH = table.Column<string>(type: "text", nullable: true),
                    TIMESTAMP_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: true),
                    STATE = table.Column<string>(type: "text", nullable: true),
                    RESULT = table.Column<string>(type: "text", nullable: true),
                    DEBUG_COMMENT = table.Column<string>(type: "text", nullable: true),
                    PAYLOAD = table.Column<string>(type: "text", nullable: true),
                    SCRIPT_RAW = table.Column<string>(type: "text", nullable: true),
                    FEE_RAW = table.Column<string>(type: "text", nullable: true),
                    EXPIRATION = table.Column<long>(type: "bigint", nullable: true),
                    GAS_PRICE_RAW = table.Column<string>(type: "text", nullable: true),
                    GAS_LIMIT_RAW = table.Column<string>(type: "text", nullable: true),
                    SENDER = table.Column<string>(type: "text", nullable: true),
                    GAS_PAYER = table.Column<string>(type: "text", nullable: true),
                    GAS_TARGET = table.Column<string>(type: "text", nullable: true),
                    CANONICAL_STATUS = table.Column<string>(type: "text", nullable: true),
                    RPC_RESPONSE_JSON = table.Column<string>(type: "text", nullable: true),
                    BLOCK_RESPONSE_JSON = table.Column<string>(type: "text", nullable: true),
                    CAPTURED_AT_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    UPDATED_AT_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    LAST_SEEN_AT_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RejectedTransactionCandidates", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RejectedTransactionCandidates_CAPTURED_AT_UNIX_SECONDS",
                table: "RejectedTransactionCandidates",
                column: "CAPTURED_AT_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_RejectedTransactionCandidates_HASH",
                table: "RejectedTransactionCandidates",
                column: "HASH");

            migrationBuilder.CreateIndex(
                name: "IX_RejectedTransactionCandidates_NEXUS_CHAIN_HASH",
                table: "RejectedTransactionCandidates",
                columns: new[] { "NEXUS", "CHAIN", "HASH" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RejectedTransactionCandidates");
        }
    }
}
