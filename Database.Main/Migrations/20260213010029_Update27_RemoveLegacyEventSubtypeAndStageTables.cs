using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update27_RemoveLegacyEventSubtypeAndStageTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChainEvents");

            migrationBuilder.DropTable(
                name: "GasEvents");

            migrationBuilder.DropTable(
                name: "HashEvents");

            migrationBuilder.DropTable(
                name: "InfusionEvents");

            migrationBuilder.DropTable(
                name: "MarketEventFiatPrices");

            migrationBuilder.DropTable(
                name: "OrganizationEvents");

            migrationBuilder.DropTable(
                name: "SaleEvents");

            migrationBuilder.DropTable(
                name: "StringEvents");

            migrationBuilder.DropTable(
                name: "TokenEvents");

            migrationBuilder.DropTable(
                name: "TransactionSettleEvents");

            migrationBuilder.DropTable(
                name: "MarketEvents");

            migrationBuilder.DropTable(
                name: "SaleEventKinds");

            migrationBuilder.DropTable(
                name: "MarketEventKinds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChainEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    NAME = table.Column<string>(type: "text", nullable: true),
                    VALUE = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChainEvents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ChainEvents_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChainEvents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GasEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    AMOUNT = table.Column<string>(type: "text", nullable: true),
                    FEE = table.Column<string>(type: "text", nullable: true),
                    PRICE = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GasEvents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_GasEvents_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GasEvents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HashEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    HASH = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HashEvents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HashEvents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InfusionEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BaseTokenId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    InfusedTokenId = table.Column<int>(type: "integer", nullable: false),
                    InfusionId = table.Column<int>(type: "integer", nullable: true),
                    INFUSED_VALUE = table.Column<string>(type: "text", nullable: true),
                    INFUSED_VALUE_RAW = table.Column<string>(type: "text", nullable: true),
                    TOKEN_ID = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfusionEvents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_InfusionEvents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InfusionEvents_Infusions_InfusionId",
                        column: x => x.InfusionId,
                        principalTable: "Infusions",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_InfusionEvents_Tokens_BaseTokenId",
                        column: x => x.BaseTokenId,
                        principalTable: "Tokens",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InfusionEvents_Tokens_InfusedTokenId",
                        column: x => x.InfusedTokenId,
                        principalTable: "Tokens",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketEventKinds",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    NAME = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketEventKinds", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MarketEventKinds_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationEvents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_OrganizationEvents_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationEvents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationEvents_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleEventKinds",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    NAME = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleEventKinds", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SaleEventKinds_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StringEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    STRING_VALUE = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StringEvents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_StringEvents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TokenEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    TokenId = table.Column<int>(type: "integer", nullable: false),
                    CHAIN_NAME = table.Column<string>(type: "text", nullable: true),
                    VALUE = table.Column<string>(type: "text", nullable: true),
                    VALUE_RAW = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenEvents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TokenEvents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TokenEvents_Tokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "Tokens",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransactionSettleEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    PlatformId = table.Column<int>(type: "integer", nullable: false),
                    HASH = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionSettleEvents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TransactionSettleEvents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransactionSettleEvents_Platforms_PlatformId",
                        column: x => x.PlatformId,
                        principalTable: "Platforms",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BaseTokenId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    MarketEventKindId = table.Column<int>(type: "integer", nullable: false),
                    QuoteTokenId = table.Column<int>(type: "integer", nullable: false),
                    END_PRICE = table.Column<string>(type: "text", nullable: true),
                    MARKET_ID = table.Column<string>(type: "text", nullable: true),
                    PRICE = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketEvents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MarketEvents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketEvents_MarketEventKinds_MarketEventKindId",
                        column: x => x.MarketEventKindId,
                        principalTable: "MarketEventKinds",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketEvents_Tokens_BaseTokenId",
                        column: x => x.BaseTokenId,
                        principalTable: "Tokens",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MarketEvents_Tokens_QuoteTokenId",
                        column: x => x.QuoteTokenId,
                        principalTable: "Tokens",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SaleEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    SaleEventKindId = table.Column<int>(type: "integer", nullable: false),
                    HASH = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleEvents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SaleEvents_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaleEvents_SaleEventKinds_SaleEventKindId",
                        column: x => x.SaleEventKindId,
                        principalTable: "SaleEventKinds",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketEventFiatPrices",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MarketEventId = table.Column<int>(type: "integer", nullable: false),
                    FIAT_NAME = table.Column<string>(type: "text", nullable: true),
                    PRICE_END_USD = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_USD = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketEventFiatPrices", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MarketEventFiatPrices_MarketEvents_MarketEventId",
                        column: x => x.MarketEventId,
                        principalTable: "MarketEvents",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChainEvents_ChainId",
                table: "ChainEvents",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_ChainEvents_EventId",
                table: "ChainEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GasEvents_AddressId",
                table: "GasEvents",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_GasEvents_EventId",
                table: "GasEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HashEvents_EventId",
                table: "HashEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InfusionEvents_BaseTokenId",
                table: "InfusionEvents",
                column: "BaseTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_InfusionEvents_EventId",
                table: "InfusionEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InfusionEvents_InfusedTokenId",
                table: "InfusionEvents",
                column: "InfusedTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_InfusionEvents_InfusionId",
                table: "InfusionEvents",
                column: "InfusionId");

            migrationBuilder.CreateIndex(
                name: "IX_InfusionEvents_TOKEN_ID",
                table: "InfusionEvents",
                column: "TOKEN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MarketEventFiatPrices_MarketEventId",
                table: "MarketEventFiatPrices",
                column: "MarketEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketEventFiatPrices_PRICE_END_USD_PRICE_USD",
                table: "MarketEventFiatPrices",
                columns: new[] { "PRICE_END_USD", "PRICE_USD" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketEventKinds_ChainId_NAME",
                table: "MarketEventKinds",
                columns: new[] { "ChainId", "NAME" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketEventKinds_NAME",
                table: "MarketEventKinds",
                column: "NAME");

            migrationBuilder.CreateIndex(
                name: "IX_MarketEvents_BaseTokenId",
                table: "MarketEvents",
                column: "BaseTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketEvents_EventId",
                table: "MarketEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketEvents_MarketEventKindId",
                table: "MarketEvents",
                column: "MarketEventKindId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketEvents_QuoteTokenId",
                table: "MarketEvents",
                column: "QuoteTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationEvents_AddressId",
                table: "OrganizationEvents",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationEvents_EventId",
                table: "OrganizationEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationEvents_OrganizationId",
                table: "OrganizationEvents",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SaleEventKinds_ChainId_NAME",
                table: "SaleEventKinds",
                columns: new[] { "ChainId", "NAME" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleEventKinds_NAME",
                table: "SaleEventKinds",
                column: "NAME");

            migrationBuilder.CreateIndex(
                name: "IX_SaleEvents_EventId",
                table: "SaleEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaleEvents_SaleEventKindId",
                table: "SaleEvents",
                column: "SaleEventKindId");

            migrationBuilder.CreateIndex(
                name: "IX_StringEvents_EventId",
                table: "StringEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenEvents_EventId",
                table: "TokenEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenEvents_TokenId",
                table: "TokenEvents",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSettleEvents_EventId",
                table: "TransactionSettleEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSettleEvents_PlatformId",
                table: "TransactionSettleEvents",
                column: "PlatformId");
        }
    }
}
