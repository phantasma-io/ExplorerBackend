using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update28_AddForwardOnlyStakeSnapshotsAndAddressFirstTx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FIRST_TX_UNIX_SECONDS",
                table: "Addresses",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SoulMastersMonthlies",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MONTH_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    MASTERS_COUNT = table.Column<int>(type: "integer", nullable: false),
                    CAPTURED_AT_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    SOURCE = table.Column<string>(type: "text", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoulMastersMonthlies", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SoulMastersMonthlies_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StakingProgressDailies",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DATE_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    STAKED_SOUL_RAW = table.Column<string>(type: "text", nullable: true),
                    SOUL_SUPPLY_RAW = table.Column<string>(type: "text", nullable: true),
                    STAKERS_COUNT = table.Column<int>(type: "integer", nullable: false),
                    MASTERS_COUNT = table.Column<int>(type: "integer", nullable: false),
                    STAKING_RATIO = table.Column<decimal>(type: "numeric", nullable: false),
                    CAPTURED_AT_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    SOURCE = table.Column<string>(type: "text", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StakingProgressDailies", x => x.ID);
                    table.ForeignKey(
                        name: "FK_StakingProgressDailies_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_ChainId_FIRST_TX_UNIX_SECONDS",
                table: "Addresses",
                columns: new[] { "ChainId", "FIRST_TX_UNIX_SECONDS" });

            migrationBuilder.CreateIndex(
                name: "IX_SoulMastersMonthlies_ChainId_MONTH_UNIX_SECONDS",
                table: "SoulMastersMonthlies",
                columns: new[] { "ChainId", "MONTH_UNIX_SECONDS" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StakingProgressDailies_ChainId_DATE_UNIX_SECONDS",
                table: "StakingProgressDailies",
                columns: new[] { "ChainId", "DATE_UNIX_SECONDS" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SoulMastersMonthlies");

            migrationBuilder.DropTable(
                name: "StakingProgressDailies");

            migrationBuilder.DropIndex(
                name: "IX_Addresses_ChainId_FIRST_TX_UNIX_SECONDS",
                table: "Addresses");

            migrationBuilder.DropColumn(
                name: "FIRST_TX_UNIX_SECONDS",
                table: "Addresses");
        }
    }
}
