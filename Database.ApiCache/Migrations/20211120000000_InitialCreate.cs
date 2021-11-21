using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Database.ApiCache.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Chains",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SHORT_NAME = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chains", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Blocks",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HEIGHT = table.Column<string>(type: "text", nullable: true),
                    TIMESTAMP = table.Column<long>(type: "bigint", nullable: false),
                    DATA = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocks", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Blocks_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HASH = table.Column<string>(type: "text", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Contracts_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Nfts",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TOKEN_ID = table.Column<string>(type: "text", nullable: true),
                    CHAIN_API_RESPONSE = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    CHAIN_API_RESPONSE_DM_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    OFFCHAIN_API_RESPONSE = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    OFFCHAIN_API_RESPONSE_DM_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nfts", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Nfts_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_ChainId_HEIGHT",
                table: "Blocks",
                columns: new[] { "ChainId", "HEIGHT" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chains_SHORT_NAME",
                table: "Chains",
                column: "SHORT_NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ChainId_HASH",
                table: "Contracts",
                columns: new[] { "ChainId", "HASH" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_ContractId_TOKEN_ID",
                table: "Nfts",
                columns: new[] { "ContractId", "TOKEN_ID" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Blocks");

            migrationBuilder.DropTable(
                name: "Nfts");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "Chains");
        }
    }
}
