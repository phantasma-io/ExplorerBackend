using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Database.Main.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FiatExchangeRates",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SYMBOL = table.Column<string>(type: "text", nullable: true),
                    USD_PRICE = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiatExchangeRates", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SeriesModes",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MODE_NAME = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesModes", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ADDRESS = table.Column<string>(type: "text", nullable: true),
                    ADDRESS_NAME = table.Column<string>(type: "text", nullable: true),
                    USER_NAME = table.Column<string>(type: "text", nullable: true),
                    USER_TITLE = table.Column<string>(type: "text", nullable: true),
                    NAME_LAST_UPDATED_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Blocks",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HEIGHT = table.Column<string>(type: "text", nullable: true),
                    TIMESTAMP_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocks", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HASH = table.Column<string>(type: "text", nullable: true),
                    INDEX = table.Column<int>(type: "integer", nullable: false),
                    BlockId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Transactions_Blocks_BlockId",
                        column: x => x.BlockId,
                        principalTable: "Blocks",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Chains",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "text", nullable: true),
                    CURRENT_HEIGHT = table.Column<string>(type: "text", nullable: true),
                    MainTokenId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chains", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "text", nullable: true),
                    HASH = table.Column<string>(type: "text", nullable: true),
                    SYMBOL = table.Column<string>(type: "text", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    TokenId = table.Column<int>(type: "integer", nullable: true)
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
                name: "EventKinds",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "text", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventKinds", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EventKinds_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Serieses",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    SERIES_ID = table.Column<string>(type: "text", nullable: true),
                    CURRENT_SUPPLY = table.Column<int>(type: "integer", nullable: false),
                    MAX_SUPPLY = table.Column<int>(type: "integer", nullable: false),
                    SeriesModeId = table.Column<int>(type: "integer", nullable: true),
                    NAME = table.Column<string>(type: "text", nullable: true),
                    DESCRIPTION = table.Column<string>(type: "text", nullable: true),
                    IMAGE = table.Column<string>(type: "text", nullable: true),
                    ROYALTIES = table.Column<decimal>(type: "numeric", nullable: false),
                    TYPE = table.Column<int>(type: "integer", nullable: false),
                    ATTR_TYPE_1 = table.Column<string>(type: "text", nullable: true),
                    ATTR_VALUE_1 = table.Column<string>(type: "text", nullable: true),
                    ATTR_TYPE_2 = table.Column<string>(type: "text", nullable: true),
                    ATTR_VALUE_2 = table.Column<string>(type: "text", nullable: true),
                    ATTR_TYPE_3 = table.Column<string>(type: "text", nullable: true),
                    ATTR_VALUE_3 = table.Column<string>(type: "text", nullable: true),
                    HAS_LOCKED = table.Column<bool>(type: "boolean", nullable: false),
                    NSFW = table.Column<bool>(type: "boolean", nullable: true),
                    BLACKLISTED = table.Column<bool>(type: "boolean", nullable: true),
                    DM_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    CreatorAddressId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Serieses", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Serieses_Addresses_CreatorAddressId",
                        column: x => x.CreatorAddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Serieses_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Serieses_SeriesModes_SeriesModeId",
                        column: x => x.SeriesModeId,
                        principalTable: "SeriesModes",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Tokens",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SYMBOL = table.Column<string>(type: "text", nullable: true),
                    FUNGIBLE = table.Column<bool>(type: "boolean", nullable: true),
                    DECIMALS = table.Column<int>(type: "integer", nullable: true),
                    PRICE_USD = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_EUR = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_GBP = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_JPY = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_CAD = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_AUD = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_CNY = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_RUB = table.Column<decimal>(type: "numeric", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    Chain2Id = table.Column<int>(type: "integer", nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tokens", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Tokens_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tokens_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Nfts",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DM_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    TOKEN_ID = table.Column<string>(type: "text", nullable: true),
                    TOKEN_URI = table.Column<string>(type: "text", nullable: true),
                    DESCRIPTION = table.Column<string>(type: "text", nullable: true),
                    NAME = table.Column<string>(type: "text", nullable: true),
                    ROM = table.Column<string>(type: "text", nullable: true),
                    RAM = table.Column<string>(type: "text", nullable: true),
                    IMAGE = table.Column<string>(type: "text", nullable: true),
                    VIDEO = table.Column<string>(type: "text", nullable: true),
                    INFO_URL = table.Column<string>(type: "text", nullable: true),
                    MINT_DATE_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    MINT_NUMBER = table.Column<int>(type: "integer", nullable: false),
                    OFFCHAIN_API_RESPONSE = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    CHAIN_API_RESPONSE = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    BURNED = table.Column<bool>(type: "boolean", nullable: true),
                    NSFW = table.Column<bool>(type: "boolean", nullable: false),
                    BLACKLISTED = table.Column<bool>(type: "boolean", nullable: false),
                    VIEW_COUNT = table.Column<int>(type: "integer", nullable: false),
                    METADATA_UPDATE = table.Column<bool>(type: "boolean", nullable: true),
                    SeriesId = table.Column<int>(type: "integer", nullable: true),
                    CreatorAddressId = table.Column<int>(type: "integer", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    InfusedIntoId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nfts", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Nfts_Addresses_CreatorAddressId",
                        column: x => x.CreatorAddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Nfts_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Nfts_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Nfts_Nfts_InfusedIntoId",
                        column: x => x.InfusedIntoId,
                        principalTable: "Nfts",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Nfts_Serieses_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "Serieses",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "TokenDailyPrices",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DATE_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    PRICE_SOUL = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_NEO = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_ETH = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_USD = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_EUR = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_GBP = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_JPY = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_CAD = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_AUD = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_CNY = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_RUB = table.Column<decimal>(type: "numeric", nullable: false),
                    TokenId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenDailyPrices", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TokenDailyPrices_Tokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "Tokens",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Infusions",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KEY = table.Column<string>(type: "text", nullable: true),
                    VALUE = table.Column<string>(type: "text", nullable: true),
                    TokenId = table.Column<int>(type: "integer", nullable: true),
                    NftId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Infusions", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Infusions_Nfts_NftId",
                        column: x => x.NftId,
                        principalTable: "Nfts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Infusions_Tokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "Tokens",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "NftOwnerships",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LAST_CHANGE_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    AMOUNT = table.Column<int>(type: "integer", nullable: false),
                    NftId = table.Column<int>(type: "integer", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NftOwnerships", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NftOwnerships_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NftOwnerships_Nfts_NftId",
                        column: x => x.NftId,
                        principalTable: "Nfts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DM_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    TIMESTAMP_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    DATE_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    INDEX = table.Column<int>(type: "integer", nullable: false),
                    TOKEN_ID = table.Column<string>(type: "text", nullable: true),
                    TOKEN_AMOUNT = table.Column<int>(type: "integer", nullable: false),
                    CONTRACT_AUCTION_ID = table.Column<string>(type: "text", nullable: true),
                    PRICE = table.Column<string>(type: "text", nullable: true),
                    PRICE_USD = table.Column<decimal>(type: "numeric", nullable: false),
                    HIDDEN = table.Column<bool>(type: "boolean", nullable: false),
                    BURNED = table.Column<bool>(type: "boolean", nullable: true),
                    NSFW = table.Column<bool>(type: "boolean", nullable: false),
                    BLACKLISTED = table.Column<bool>(type: "boolean", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    SourceAddressId = table.Column<int>(type: "integer", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    TransactionId = table.Column<int>(type: "integer", nullable: true),
                    EventKindId = table.Column<int>(type: "integer", nullable: false),
                    QuoteSymbolId = table.Column<int>(type: "integer", nullable: true),
                    InfusedSymbolId = table.Column<int>(type: "integer", nullable: true),
                    INFUSED_VALUE = table.Column<string>(type: "text", nullable: true),
                    InfusionId = table.Column<int>(type: "integer", nullable: true),
                    NftId = table.Column<int>(type: "integer", nullable: true),
                    AddressID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Events_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Addresses_AddressID",
                        column: x => x.AddressID,
                        principalTable: "Addresses",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Events_Addresses_SourceAddressId",
                        column: x => x.SourceAddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Events_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_EventKinds_EventKindId",
                        column: x => x.EventKindId,
                        principalTable: "EventKinds",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Events_Infusions_InfusionId",
                        column: x => x.InfusionId,
                        principalTable: "Infusions",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Events_Nfts_NftId",
                        column: x => x.NftId,
                        principalTable: "Nfts",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Events_Tokens_InfusedSymbolId",
                        column: x => x.InfusedSymbolId,
                        principalTable: "Tokens",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Events_Tokens_QuoteSymbolId",
                        column: x => x.QuoteSymbolId,
                        principalTable: "Tokens",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Events_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_ADDRESS_NAME",
                table: "Addresses",
                column: "ADDRESS_NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_ChainId_ADDRESS",
                table: "Addresses",
                columns: new[] { "ChainId", "ADDRESS" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_NAME_LAST_UPDATED_UNIX_SECONDS",
                table: "Addresses",
                column: "NAME_LAST_UPDATED_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_USER_NAME",
                table: "Addresses",
                column: "USER_NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_ChainId_HEIGHT",
                table: "Blocks",
                columns: new[] { "ChainId", "HEIGHT" });

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_TIMESTAMP_UNIX_SECONDS",
                table: "Blocks",
                column: "TIMESTAMP_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_Chains_MainTokenId",
                table: "Chains",
                column: "MainTokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chains_NAME",
                table: "Chains",
                column: "NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ChainId",
                table: "Contracts",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_EventKinds_ChainId_NAME",
                table: "EventKinds",
                columns: new[] { "ChainId", "NAME" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventKinds_NAME",
                table: "EventKinds",
                column: "NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Events_AddressId",
                table: "Events",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_AddressID",
                table: "Events",
                column: "AddressID");

            migrationBuilder.CreateIndex(
                name: "IX_Events_BLACKLISTED",
                table: "Events",
                column: "BLACKLISTED");

            migrationBuilder.CreateIndex(
                name: "IX_Events_BURNED",
                table: "Events",
                column: "BURNED");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ChainId",
                table: "Events",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ContractId_TOKEN_ID",
                table: "Events",
                columns: new[] { "ContractId", "TOKEN_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_DATE_UNIX_SECONDS",
                table: "Events",
                column: "DATE_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_Events_DM_UNIX_SECONDS",
                table: "Events",
                column: "DM_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventKindId",
                table: "Events",
                column: "EventKindId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_HIDDEN",
                table: "Events",
                column: "HIDDEN");

            migrationBuilder.CreateIndex(
                name: "IX_Events_INDEX",
                table: "Events",
                column: "INDEX");

            migrationBuilder.CreateIndex(
                name: "IX_Events_InfusedSymbolId",
                table: "Events",
                column: "InfusedSymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_InfusionId",
                table: "Events",
                column: "InfusionId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_NftId",
                table: "Events",
                column: "NftId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_NSFW",
                table: "Events",
                column: "NSFW");

            migrationBuilder.CreateIndex(
                name: "IX_Events_PRICE_USD",
                table: "Events",
                column: "PRICE_USD");

            migrationBuilder.CreateIndex(
                name: "IX_Events_QuoteSymbolId",
                table: "Events",
                column: "QuoteSymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_SourceAddressId",
                table: "Events",
                column: "SourceAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_TIMESTAMP_UNIX_SECONDS",
                table: "Events",
                column: "TIMESTAMP_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_Events_TransactionId_INDEX",
                table: "Events",
                columns: new[] { "TransactionId", "INDEX" });

            migrationBuilder.CreateIndex(
                name: "IX_FiatExchangeRates_SYMBOL",
                table: "FiatExchangeRates",
                column: "SYMBOL",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Infusions_KEY",
                table: "Infusions",
                column: "KEY");

            migrationBuilder.CreateIndex(
                name: "IX_Infusions_NftId",
                table: "Infusions",
                column: "NftId");

            migrationBuilder.CreateIndex(
                name: "IX_Infusions_TokenId",
                table: "Infusions",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_NftOwnerships_AddressId_NftId",
                table: "NftOwnerships",
                columns: new[] { "AddressId", "NftId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NftOwnerships_NftId",
                table: "NftOwnerships",
                column: "NftId");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_BLACKLISTED",
                table: "Nfts",
                column: "BLACKLISTED");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_BURNED",
                table: "Nfts",
                column: "BURNED");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_ChainId",
                table: "Nfts",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_ContractId_TOKEN_ID",
                table: "Nfts",
                columns: new[] { "ContractId", "TOKEN_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_ContractId_TOKEN_URI",
                table: "Nfts",
                columns: new[] { "ContractId", "TOKEN_URI" });

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_CreatorAddressId",
                table: "Nfts",
                column: "CreatorAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_DESCRIPTION",
                table: "Nfts",
                column: "DESCRIPTION");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_DM_UNIX_SECONDS",
                table: "Nfts",
                column: "DM_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_InfusedIntoId",
                table: "Nfts",
                column: "InfusedIntoId");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_METADATA_UPDATE",
                table: "Nfts",
                column: "METADATA_UPDATE");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_MINT_DATE_UNIX_SECONDS",
                table: "Nfts",
                column: "MINT_DATE_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_MINT_NUMBER",
                table: "Nfts",
                column: "MINT_NUMBER");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_NAME",
                table: "Nfts",
                column: "NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_NSFW",
                table: "Nfts",
                column: "NSFW");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_SeriesId",
                table: "Nfts",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_BLACKLISTED",
                table: "Serieses",
                column: "BLACKLISTED");

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_ContractId_SERIES_ID",
                table: "Serieses",
                columns: new[] { "ContractId", "SERIES_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_CreatorAddressId",
                table: "Serieses",
                column: "CreatorAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_DESCRIPTION",
                table: "Serieses",
                column: "DESCRIPTION");

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_DM_UNIX_SECONDS",
                table: "Serieses",
                column: "DM_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_HAS_LOCKED",
                table: "Serieses",
                column: "HAS_LOCKED");

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_NAME",
                table: "Serieses",
                column: "NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_NSFW",
                table: "Serieses",
                column: "NSFW");

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_SERIES_ID",
                table: "Serieses",
                column: "SERIES_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_SeriesModeId",
                table: "Serieses",
                column: "SeriesModeId");

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_TYPE",
                table: "Serieses",
                column: "TYPE");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesModes_MODE_NAME",
                table: "SeriesModes",
                column: "MODE_NAME");

            migrationBuilder.CreateIndex(
                name: "IX_TokenDailyPrices_DATE_UNIX_SECONDS",
                table: "TokenDailyPrices",
                column: "DATE_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_TokenDailyPrices_TokenId",
                table: "TokenDailyPrices",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_ChainId_ContractId_SYMBOL",
                table: "Tokens",
                columns: new[] { "ChainId", "ContractId", "SYMBOL" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_ContractId",
                table: "Tokens",
                column: "ContractId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_SYMBOL",
                table: "Tokens",
                column: "SYMBOL");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BlockId_INDEX",
                table: "Transactions",
                columns: new[] { "BlockId", "INDEX" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_HASH",
                table: "Transactions",
                column: "HASH");

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_Chains_ChainId",
                table: "Addresses",
                column: "ChainId",
                principalTable: "Chains",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Blocks_Chains_ChainId",
                table: "Blocks",
                column: "ChainId",
                principalTable: "Chains",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Chains_Tokens_MainTokenId",
                table: "Chains",
                column: "MainTokenId",
                principalTable: "Tokens",
                principalColumn: "ID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chains_Tokens_MainTokenId",
                table: "Chains");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "FiatExchangeRates");

            migrationBuilder.DropTable(
                name: "NftOwnerships");

            migrationBuilder.DropTable(
                name: "TokenDailyPrices");

            migrationBuilder.DropTable(
                name: "EventKinds");

            migrationBuilder.DropTable(
                name: "Infusions");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Nfts");

            migrationBuilder.DropTable(
                name: "Blocks");

            migrationBuilder.DropTable(
                name: "Serieses");

            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "SeriesModes");

            migrationBuilder.DropTable(
                name: "Tokens");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "Chains");
        }
    }
}
