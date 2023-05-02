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
                name: "AddressValidatorKinds",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressValidatorKinds", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Chains",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "text", nullable: true),
                    CURRENT_HEIGHT = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chains", x => x.ID);
                });

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
                name: "Oracles",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    URL = table.Column<string>(type: "text", nullable: true),
                    CONTENT = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Oracles", x => x.ID);
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
                name: "SignatureKinds",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureKinds", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TokenLogoTypes",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenLogoTypes", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TransactionStates",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionStates", x => x.ID);
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
                name: "MarketEventKinds",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "text", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false)
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
                name: "SaleEventKinds",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "text", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false)
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
                name: "AddressBalances",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TokenId = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    AMOUNT = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressBalances", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AddressBalances_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
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
                    NAME_LAST_UPDATED_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    STAKE = table.Column<string>(type: "text", nullable: true),
                    UNCLAIMED = table.Column<string>(type: "text", nullable: true),
                    RELAY = table.Column<string>(type: "text", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    AddressValidatorKindId = table.Column<int>(type: "integer", nullable: true),
                    OrganizationId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Addresses_AddressValidatorKinds_AddressValidatorKindId",
                        column: x => x.AddressValidatorKindId,
                        principalTable: "AddressValidatorKinds",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Addresses_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AddressStakes",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    AMOUNT = table.Column<string>(type: "text", nullable: true),
                    TIME = table.Column<long>(type: "bigint", nullable: false),
                    UNCLAIMED = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressStakes", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AddressStakes_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AddressStorages",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    AVAILABLE = table.Column<long>(type: "bigint", nullable: false),
                    USED = table.Column<long>(type: "bigint", nullable: false),
                    AVATAR = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressStorages", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AddressStorages_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Blocks",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HEIGHT = table.Column<string>(type: "text", nullable: true),
                    TIMESTAMP_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    HASH = table.Column<string>(type: "text", nullable: true),
                    PREVIOUS_HASH = table.Column<string>(type: "text", nullable: true),
                    PROTOCOL = table.Column<int>(type: "integer", nullable: false),
                    ChainAddressId = table.Column<int>(type: "integer", nullable: false),
                    ValidatorAddressId = table.Column<int>(type: "integer", nullable: false),
                    REWARD = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocks", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Blocks_Addresses_ChainAddressId",
                        column: x => x.ChainAddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Blocks_Addresses_ValidatorAddressId",
                        column: x => x.ValidatorAddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Blocks_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BlockOracles",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OracleId = table.Column<int>(type: "integer", nullable: false),
                    BlockId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockOracles", x => x.ID);
                    table.ForeignKey(
                        name: "FK_BlockOracles_Blocks_BlockId",
                        column: x => x.BlockId,
                        principalTable: "Blocks",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlockOracles_Oracles_OracleId",
                        column: x => x.OracleId,
                        principalTable: "Oracles",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HASH = table.Column<string>(type: "text", nullable: true),
                    INDEX = table.Column<int>(type: "integer", nullable: false),
                    BlockId = table.Column<int>(type: "integer", nullable: false),
                    TIMESTAMP_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    PAYLOAD = table.Column<string>(type: "text", nullable: true),
                    SCRIPT_RAW = table.Column<string>(type: "text", nullable: true),
                    RESULT = table.Column<string>(type: "text", nullable: true),
                    FEE = table.Column<string>(type: "text", nullable: true),
                    EXPIRATION = table.Column<long>(type: "bigint", nullable: false),
                    StateId = table.Column<int>(type: "integer", nullable: false),
                    GAS_PRICE = table.Column<string>(type: "text", nullable: true),
                    GAS_LIMIT = table.Column<string>(type: "text", nullable: true),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    GasPayerId = table.Column<int>(type: "integer", nullable: false),
                    GasTargetId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Transactions_Addresses_GasPayerId",
                        column: x => x.GasPayerId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_Addresses_GasTargetId",
                        column: x => x.GasTargetId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_Addresses_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_Blocks_BlockId",
                        column: x => x.BlockId,
                        principalTable: "Blocks",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Transactions_TransactionStates_StateId",
                        column: x => x.StateId,
                        principalTable: "TransactionStates",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AddressTransactions",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    TransactionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressTransactions", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AddressTransactions_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AddressTransactions_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Signatures",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SignatureKindId = table.Column<int>(type: "integer", nullable: false),
                    DATA = table.Column<string>(type: "text", nullable: true),
                    TransactionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signatures", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Signatures_SignatureKinds_SignatureKindId",
                        column: x => x.SignatureKindId,
                        principalTable: "SignatureKinds",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Signatures_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AddressEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressEvents", x => x.ID);
                    table.ForeignKey(
                        name: "FK_AddressEvents_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChainEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "text", nullable: true),
                    VALUE = table.Column<string>(type: "text", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "ContractMethods",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    METHODS = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    TIMESTAMP_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractMethods", x => x.ID);
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
                    SCRIPT_RAW = table.Column<string>(type: "text", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: true),
                    ContractMethodId = table.Column<int>(type: "integer", nullable: true),
                    LAST_UPDATED_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    TokenId = table.Column<int>(type: "integer", nullable: true),
                    CreateEventId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Contracts_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Contracts_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Contracts_ContractMethods_ContractMethodId",
                        column: x => x.ContractMethodId,
                        principalTable: "ContractMethods",
                        principalColumn: "ID");
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
                    BURNED = table.Column<bool>(type: "boolean", nullable: true),
                    NSFW = table.Column<bool>(type: "boolean", nullable: false),
                    BLACKLISTED = table.Column<bool>(type: "boolean", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    TransactionId = table.Column<int>(type: "integer", nullable: false),
                    EventKindId = table.Column<int>(type: "integer", nullable: false),
                    NftId = table.Column<int>(type: "integer", nullable: true)
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
                        name: "FK_Events_Nfts_NftId",
                        column: x => x.NftId,
                        principalTable: "Nfts",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_Events_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
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
                name: "GasEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRICE = table.Column<string>(type: "text", nullable: true),
                    AMOUNT = table.Column<string>(type: "text", nullable: true),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false)
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
                    HASH = table.Column<string>(type: "text", nullable: true),
                    EventId = table.Column<int>(type: "integer", nullable: false)
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
                name: "Organizations",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ORGANIZATION_ID = table.Column<string>(type: "text", nullable: true),
                    NAME = table.Column<string>(type: "text", nullable: true),
                    CreateEventId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Organizations_Events_CreateEventId",
                        column: x => x.CreateEventId,
                        principalTable: "Events",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Platforms",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NAME = table.Column<string>(type: "text", nullable: true),
                    CHAIN = table.Column<string>(type: "text", nullable: true),
                    FUEL = table.Column<string>(type: "text", nullable: true),
                    HIDDEN = table.Column<bool>(type: "boolean", nullable: false),
                    CreateEventId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Platforms", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Platforms_Events_CreateEventId",
                        column: x => x.CreateEventId,
                        principalTable: "Events",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "SaleEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HASH = table.Column<string>(type: "text", nullable: true),
                    SaleEventKindId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false)
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
                name: "StringEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    STRING_VALUE = table.Column<string>(type: "text", nullable: true),
                    EventId = table.Column<int>(type: "integer", nullable: false)
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
                name: "Tokens",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SYMBOL = table.Column<string>(type: "text", nullable: true),
                    FUNGIBLE = table.Column<bool>(type: "boolean", nullable: false),
                    TRANSFERABLE = table.Column<bool>(type: "boolean", nullable: false),
                    FINITE = table.Column<bool>(type: "boolean", nullable: false),
                    DIVISIBLE = table.Column<bool>(type: "boolean", nullable: false),
                    FUEL = table.Column<bool>(type: "boolean", nullable: false),
                    STAKABLE = table.Column<bool>(type: "boolean", nullable: false),
                    FIAT = table.Column<bool>(type: "boolean", nullable: false),
                    SWAPPABLE = table.Column<bool>(type: "boolean", nullable: false),
                    BURNABLE = table.Column<bool>(type: "boolean", nullable: false),
                    DECIMALS = table.Column<int>(type: "integer", nullable: false),
                    CURRENT_SUPPLY = table.Column<string>(type: "text", nullable: true),
                    MAX_SUPPLY = table.Column<string>(type: "text", nullable: true),
                    BURNED_SUPPLY = table.Column<string>(type: "text", nullable: true),
                    SCRIPT_RAW = table.Column<string>(type: "text", nullable: true),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<int>(type: "integer", nullable: false),
                    PRICE_USD = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_EUR = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_GBP = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_JPY = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_CAD = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_AUD = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_CNY = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_RUB = table.Column<decimal>(type: "numeric", nullable: false),
                    ChainId = table.Column<int>(type: "integer", nullable: false),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    CreateEventId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tokens", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Tokens_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tokens_Addresses_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tokens_Chains_ChainId",
                        column: x => x.ChainId,
                        principalTable: "Chains",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tokens_Events_CreateEventId",
                        column: x => x.CreateEventId,
                        principalTable: "Events",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "OrganizationAddresses",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationAddresses", x => x.ID);
                    table.ForeignKey(
                        name: "FK_OrganizationAddresses_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationAddresses_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false)
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
                name: "PlatformInterops",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlatformId = table.Column<int>(type: "integer", nullable: false),
                    LocalAddressId = table.Column<int>(type: "integer", nullable: false),
                    EXTERNAL = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformInterops", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PlatformInterops_Addresses_LocalAddressId",
                        column: x => x.LocalAddressId,
                        principalTable: "Addresses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlatformInterops_Platforms_PlatformId",
                        column: x => x.PlatformId,
                        principalTable: "Platforms",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlatformTokens",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlatformId = table.Column<int>(type: "integer", nullable: false),
                    NAME = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformTokens", x => x.ID);
                    table.ForeignKey(
                        name: "FK_PlatformTokens_Platforms_PlatformId",
                        column: x => x.PlatformId,
                        principalTable: "Platforms",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransactionSettleEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HASH = table.Column<string>(type: "text", nullable: true),
                    PlatformId = table.Column<int>(type: "integer", nullable: false),
                    EventId = table.Column<int>(type: "integer", nullable: false)
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
                name: "Externals",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlatformId = table.Column<int>(type: "integer", nullable: false),
                    TokenId = table.Column<int>(type: "integer", nullable: false),
                    HASH = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Externals", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Externals_Platforms_PlatformId",
                        column: x => x.PlatformId,
                        principalTable: "Platforms",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Externals_Tokens_TokenId",
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
                name: "MarketEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BaseTokenId = table.Column<int>(type: "integer", nullable: false),
                    QuoteTokenId = table.Column<int>(type: "integer", nullable: false),
                    MarketEventKindId = table.Column<int>(type: "integer", nullable: false),
                    MARKET_ID = table.Column<string>(type: "text", nullable: true),
                    PRICE = table.Column<string>(type: "text", nullable: true),
                    END_PRICE = table.Column<string>(type: "text", nullable: true),
                    EventId = table.Column<int>(type: "integer", nullable: false)
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
                name: "TokenEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TokenId = table.Column<int>(type: "integer", nullable: false),
                    VALUE = table.Column<string>(type: "text", nullable: true),
                    CHAIN_NAME = table.Column<string>(type: "text", nullable: true),
                    EventId = table.Column<int>(type: "integer", nullable: false)
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
                name: "TokenLogos",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TokenId = table.Column<int>(type: "integer", nullable: false),
                    TokenLogoTypeId = table.Column<int>(type: "integer", nullable: false),
                    URL = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenLogos", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TokenLogos_TokenLogoTypes_TokenLogoTypeId",
                        column: x => x.TokenLogoTypeId,
                        principalTable: "TokenLogoTypes",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TokenLogos_Tokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "Tokens",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TokenPriceStates",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TokenId = table.Column<int>(type: "integer", nullable: false),
                    LAST_CHECK_DATE_UNIX_SECONDS = table.Column<long>(type: "bigint", nullable: false),
                    COIN_GECKO = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenPriceStates", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TokenPriceStates_Tokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "Tokens",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InfusionEvents",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TOKEN_ID = table.Column<string>(type: "text", nullable: true),
                    BaseTokenId = table.Column<int>(type: "integer", nullable: false),
                    InfusedTokenId = table.Column<int>(type: "integer", nullable: false),
                    INFUSED_VALUE = table.Column<string>(type: "text", nullable: true),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    InfusionId = table.Column<int>(type: "integer", nullable: true)
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
                name: "MarketEventFiatPrices",
                columns: table => new
                {
                    ID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PRICE_USD = table.Column<decimal>(type: "numeric", nullable: false),
                    PRICE_END_USD = table.Column<decimal>(type: "numeric", nullable: false),
                    FIAT_NAME = table.Column<string>(type: "text", nullable: true),
                    MarketEventId = table.Column<int>(type: "integer", nullable: false)
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
                name: "IX_AddressBalances_AddressId",
                table: "AddressBalances",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressBalances_ChainId",
                table: "AddressBalances",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressBalances_TokenId",
                table: "AddressBalances",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_ADDRESS_ADDRESS_NAME",
                table: "Addresses",
                columns: new[] { "ADDRESS", "ADDRESS_NAME" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_AddressValidatorKindId",
                table: "Addresses",
                column: "AddressValidatorKindId");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_ChainId_ADDRESS",
                table: "Addresses",
                columns: new[] { "ChainId", "ADDRESS" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_ChainId_NAME_LAST_UPDATED_UNIX_SECONDS",
                table: "Addresses",
                columns: new[] { "ChainId", "NAME_LAST_UPDATED_UNIX_SECONDS" });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_OrganizationId",
                table: "Addresses",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressEvents_AddressId",
                table: "AddressEvents",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressEvents_EventId",
                table: "AddressEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AddressStakes_AddressId",
                table: "AddressStakes",
                column: "AddressId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AddressStorages_AddressId",
                table: "AddressStorages",
                column: "AddressId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AddressTransactions_AddressId",
                table: "AddressTransactions",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressTransactions_TransactionId",
                table: "AddressTransactions",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressValidatorKinds_NAME",
                table: "AddressValidatorKinds",
                column: "NAME");

            migrationBuilder.CreateIndex(
                name: "IX_BlockOracles_BlockId",
                table: "BlockOracles",
                column: "BlockId");

            migrationBuilder.CreateIndex(
                name: "IX_BlockOracles_OracleId",
                table: "BlockOracles",
                column: "OracleId");

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_ChainAddressId",
                table: "Blocks",
                column: "ChainAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_ChainId_HEIGHT",
                table: "Blocks",
                columns: new[] { "ChainId", "HEIGHT" });

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_HASH",
                table: "Blocks",
                column: "HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_TIMESTAMP_UNIX_SECONDS",
                table: "Blocks",
                column: "TIMESTAMP_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_ValidatorAddressId",
                table: "Blocks",
                column: "ValidatorAddressId");

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
                name: "IX_Chains_NAME",
                table: "Chains",
                column: "NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractMethods_ContractId",
                table: "ContractMethods",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_AddressId",
                table: "Contracts",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ChainId",
                table: "Contracts",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractMethodId",
                table: "Contracts",
                column: "ContractMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CreateEventId",
                table: "Contracts",
                column: "CreateEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_TokenId",
                table: "Contracts",
                column: "TokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventKinds_ChainId_NAME",
                table: "EventKinds",
                columns: new[] { "ChainId", "NAME" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_AddressId",
                table: "Events",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_BURNED_EventKindId",
                table: "Events",
                columns: new[] { "BURNED", "EventKindId" });

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
                name: "IX_Events_EventKindId",
                table: "Events",
                column: "EventKindId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_NftId",
                table: "Events",
                column: "NftId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_TIMESTAMP_UNIX_SECONDS",
                table: "Events",
                column: "TIMESTAMP_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_Events_TransactionId",
                table: "Events",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Externals_PlatformId",
                table: "Externals",
                column: "PlatformId");

            migrationBuilder.CreateIndex(
                name: "IX_Externals_TokenId",
                table: "Externals",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_FiatExchangeRates_SYMBOL",
                table: "FiatExchangeRates",
                column: "SYMBOL",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiatExchangeRates_SYMBOL_USD_PRICE",
                table: "FiatExchangeRates",
                columns: new[] { "SYMBOL", "USD_PRICE" });

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
                name: "IX_NftOwnerships_AddressId_NftId",
                table: "NftOwnerships",
                columns: new[] { "AddressId", "NftId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NftOwnerships_LAST_CHANGE_UNIX_SECONDS",
                table: "NftOwnerships",
                column: "LAST_CHANGE_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_NftOwnerships_NftId",
                table: "NftOwnerships",
                column: "NftId");

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
                name: "IX_Nfts_DM_UNIX_SECONDS",
                table: "Nfts",
                column: "DM_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_InfusedIntoId",
                table: "Nfts",
                column: "InfusedIntoId");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_SeriesId",
                table: "Nfts",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_Nfts_TOKEN_ID",
                table: "Nfts",
                column: "TOKEN_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Oracles_URL_CONTENT",
                table: "Oracles",
                columns: new[] { "URL", "CONTENT" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAddresses_AddressId",
                table: "OrganizationAddresses",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationAddresses_OrganizationId",
                table: "OrganizationAddresses",
                column: "OrganizationId");

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
                name: "IX_Organizations_CreateEventId",
                table: "Organizations",
                column: "CreateEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_NAME",
                table: "Organizations",
                column: "NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInterops_LocalAddressId",
                table: "PlatformInterops",
                column: "LocalAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformInterops_PlatformId",
                table: "PlatformInterops",
                column: "PlatformId");

            migrationBuilder.CreateIndex(
                name: "IX_Platforms_CreateEventId",
                table: "Platforms",
                column: "CreateEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Platforms_NAME",
                table: "Platforms",
                column: "NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformTokens_NAME",
                table: "PlatformTokens",
                column: "NAME");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformTokens_PlatformId",
                table: "PlatformTokens",
                column: "PlatformId");

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
                name: "IX_Serieses_ContractId_SERIES_ID",
                table: "Serieses",
                columns: new[] { "ContractId", "SERIES_ID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_CreatorAddressId",
                table: "Serieses",
                column: "CreatorAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_SERIES_ID",
                table: "Serieses",
                column: "SERIES_ID");

            migrationBuilder.CreateIndex(
                name: "IX_Serieses_SeriesModeId",
                table: "Serieses",
                column: "SeriesModeId");

            migrationBuilder.CreateIndex(
                name: "IX_SeriesModes_MODE_NAME",
                table: "SeriesModes",
                column: "MODE_NAME",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignatureKinds_NAME",
                table: "SignatureKinds",
                column: "NAME");

            migrationBuilder.CreateIndex(
                name: "IX_Signatures_SignatureKindId",
                table: "Signatures",
                column: "SignatureKindId");

            migrationBuilder.CreateIndex(
                name: "IX_Signatures_TransactionId",
                table: "Signatures",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_StringEvents_EventId",
                table: "StringEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenDailyPrices_DATE_UNIX_SECONDS",
                table: "TokenDailyPrices",
                column: "DATE_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_TokenDailyPrices_TokenId",
                table: "TokenDailyPrices",
                column: "TokenId");

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
                name: "IX_TokenLogos_TokenId_TokenLogoTypeId",
                table: "TokenLogos",
                columns: new[] { "TokenId", "TokenLogoTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenLogos_TokenLogoTypeId",
                table: "TokenLogos",
                column: "TokenLogoTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TokenLogoTypes_NAME",
                table: "TokenLogoTypes",
                column: "NAME");

            migrationBuilder.CreateIndex(
                name: "IX_TokenPriceStates_LAST_CHECK_DATE_UNIX_SECONDS",
                table: "TokenPriceStates",
                column: "LAST_CHECK_DATE_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_TokenPriceStates_TokenId",
                table: "TokenPriceStates",
                column: "TokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_AddressId",
                table: "Tokens",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_ChainId_ContractId_SYMBOL",
                table: "Tokens",
                columns: new[] { "ChainId", "ContractId", "SYMBOL" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_CreateEventId",
                table: "Tokens",
                column: "CreateEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_OwnerId",
                table: "Tokens",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_SYMBOL_ChainId",
                table: "Tokens",
                columns: new[] { "SYMBOL", "ChainId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BlockId_INDEX",
                table: "Transactions",
                columns: new[] { "BlockId", "INDEX" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_GasPayerId",
                table: "Transactions",
                column: "GasPayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_GasTargetId",
                table: "Transactions",
                column: "GasTargetId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_HASH",
                table: "Transactions",
                column: "HASH");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SenderId",
                table: "Transactions",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_StateId",
                table: "Transactions",
                column: "StateId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TIMESTAMP_UNIX_SECONDS",
                table: "Transactions",
                column: "TIMESTAMP_UNIX_SECONDS");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSettleEvents_EventId",
                table: "TransactionSettleEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransactionSettleEvents_PlatformId",
                table: "TransactionSettleEvents",
                column: "PlatformId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionStates_NAME",
                table: "TransactionStates",
                column: "NAME");

            migrationBuilder.AddForeignKey(
                name: "FK_AddressBalances_Addresses_AddressId",
                table: "AddressBalances",
                column: "AddressId",
                principalTable: "Addresses",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AddressBalances_Tokens_TokenId",
                table: "AddressBalances",
                column: "TokenId",
                principalTable: "Tokens",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Addresses_Organizations_OrganizationId",
                table: "Addresses",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_AddressEvents_Events_EventId",
                table: "AddressEvents",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChainEvents_Events_EventId",
                table: "ChainEvents",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractMethods_Contracts_ContractId",
                table: "ContractMethods",
                column: "ContractId",
                principalTable: "Contracts",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Events_CreateEventId",
                table: "Contracts",
                column: "CreateEventId",
                principalTable: "Events",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_Tokens_TokenId",
                table: "Contracts",
                column: "TokenId",
                principalTable: "Tokens",
                principalColumn: "ID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Blocks_Addresses_ChainAddressId",
                table: "Blocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Blocks_Addresses_ValidatorAddressId",
                table: "Blocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Addresses_AddressId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Addresses_AddressId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Nfts_Addresses_CreatorAddressId",
                table: "Nfts");

            migrationBuilder.DropForeignKey(
                name: "FK_Serieses_Addresses_CreatorAddressId",
                table: "Serieses");

            migrationBuilder.DropForeignKey(
                name: "FK_Tokens_Addresses_AddressId",
                table: "Tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Tokens_Addresses_OwnerId",
                table: "Tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Addresses_GasPayerId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Addresses_GasTargetId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Addresses_SenderId",
                table: "Transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Blocks_Chains_ChainId",
                table: "Blocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Chains_ChainId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_EventKinds_Chains_ChainId",
                table: "EventKinds");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Chains_ChainId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Nfts_Chains_ChainId",
                table: "Nfts");

            migrationBuilder.DropForeignKey(
                name: "FK_Tokens_Chains_ChainId",
                table: "Tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Tokens_TokenId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_Events_CreateEventId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractMethods_Contracts_ContractId",
                table: "ContractMethods");

            migrationBuilder.DropTable(
                name: "AddressBalances");

            migrationBuilder.DropTable(
                name: "AddressEvents");

            migrationBuilder.DropTable(
                name: "AddressStakes");

            migrationBuilder.DropTable(
                name: "AddressStorages");

            migrationBuilder.DropTable(
                name: "AddressTransactions");

            migrationBuilder.DropTable(
                name: "BlockOracles");

            migrationBuilder.DropTable(
                name: "ChainEvents");

            migrationBuilder.DropTable(
                name: "Externals");

            migrationBuilder.DropTable(
                name: "FiatExchangeRates");

            migrationBuilder.DropTable(
                name: "GasEvents");

            migrationBuilder.DropTable(
                name: "HashEvents");

            migrationBuilder.DropTable(
                name: "InfusionEvents");

            migrationBuilder.DropTable(
                name: "MarketEventFiatPrices");

            migrationBuilder.DropTable(
                name: "NftOwnerships");

            migrationBuilder.DropTable(
                name: "OrganizationAddresses");

            migrationBuilder.DropTable(
                name: "OrganizationEvents");

            migrationBuilder.DropTable(
                name: "PlatformInterops");

            migrationBuilder.DropTable(
                name: "PlatformTokens");

            migrationBuilder.DropTable(
                name: "SaleEvents");

            migrationBuilder.DropTable(
                name: "Signatures");

            migrationBuilder.DropTable(
                name: "StringEvents");

            migrationBuilder.DropTable(
                name: "TokenDailyPrices");

            migrationBuilder.DropTable(
                name: "TokenEvents");

            migrationBuilder.DropTable(
                name: "TokenLogos");

            migrationBuilder.DropTable(
                name: "TokenPriceStates");

            migrationBuilder.DropTable(
                name: "TransactionSettleEvents");

            migrationBuilder.DropTable(
                name: "Oracles");

            migrationBuilder.DropTable(
                name: "Infusions");

            migrationBuilder.DropTable(
                name: "MarketEvents");

            migrationBuilder.DropTable(
                name: "SaleEventKinds");

            migrationBuilder.DropTable(
                name: "SignatureKinds");

            migrationBuilder.DropTable(
                name: "TokenLogoTypes");

            migrationBuilder.DropTable(
                name: "Platforms");

            migrationBuilder.DropTable(
                name: "MarketEventKinds");

            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "AddressValidatorKinds");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "Chains");

            migrationBuilder.DropTable(
                name: "Tokens");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "EventKinds");

            migrationBuilder.DropTable(
                name: "Nfts");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "Serieses");

            migrationBuilder.DropTable(
                name: "Blocks");

            migrationBuilder.DropTable(
                name: "TransactionStates");

            migrationBuilder.DropTable(
                name: "SeriesModes");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "ContractMethods");
        }
    }
}
