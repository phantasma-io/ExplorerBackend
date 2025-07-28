using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update16 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert into Chains if NAME = 'main' doesn't exist
            migrationBuilder.Sql(@"
INSERT INTO ""Chains"" (""ID"", ""NAME"", ""CURRENT_HEIGHT"")
SELECT 1, 'main', 0
WHERE NOT EXISTS (
    SELECT 1 FROM ""Chains"" WHERE ""NAME"" = 'main'
);
");

            // Insert into EventKinds only if NAME doesn't exist (compare by string)
            var eventKinds = new (int Id, string Name)[]
            {
            (1, "ValueCreate"),
            (2, "TokenMint"),
            (3, "TokenStake"),
            (4, "OrganizationAdd"),
            (5, "ValidatorElect"),
            (6, "GasEscrow"),
            (7, "GasPayment"),
            (8, "TokenClaim"),
            (9, "TokenBurn"),
            (10, "CrownRewards"),
            (11, "TokenSend"),
            (12, "TokenReceive"),
            (13, "ContractUpgrade"),
            (14, "ExecutionFailure"),
            (15, "OrganizationRemove"),
            (16, "AddressUnregister"),
            (17, "AddressRegister"),
            (18, "Infusion"),
            (19, "OrderFilled"),
            (20, "AddressMigration"),
            (21, "OrderCreated"),
            (22, "OrderCancelled"),
            (23, "OrderBid"),
            (24, "Custom"),
            (25, "PollCreated"),
            (26, "PollVote"),
            (27, "PollClosed"),
            (28, "ValueUpdate"),
            (29, "MasterClaim"),
            (30, "Inflation"),
            (31, "TokenCreate"),
            (32, "FileCreate"),
            (33, "FileDelete"),
            (34, "ValidatorRemove"),
            (35, "Log"),
            (36, "ContractDeploy"),
            (37, "Unknown"),
            (38, "ChainCreate"),
            (39, "AddressLink"),
            (40, "AddressUnlink"),
            (41, "OrganizationCreate"),
            (42, "OrderClosed"),
            (43, "FeedCreate"),
            (44, "FeedUpdate"),
            (45, "ValidatorPropose"),
            (46, "ValidatorSwitch"),
            (47, "PackedNFT"),
            (48, "ChannelCreate"),
            (49, "ChannelRefill"),
            (50, "ChannelSettle"),
            (51, "LeaderboardCreate"),
            (52, "LeaderboardInsert"),
            (53, "LeaderboardReset"),
            (54, "PlatformCreate"),
            (55, "ChainSwap"),
            (56, "ContractRegister"),
            (57, "OwnerAdded"),
            (58, "OwnerRemoved"),
            (59, "DomainCreate"),
            (60, "DomainDelete"),
            (61, "TaskStart"),
            (62, "TaskStop"),
            (63, "Crowdsale"),
            (64, "ContractKill"),
            (65, "OrganizationKill"),
            (66, "Custom_V2"),
            (67, "GovernanceSetGasEvent"),
            };

            foreach (var (id, name) in eventKinds)
            {
                migrationBuilder.Sql($@"
INSERT INTO ""EventKinds"" (""ID"", ""NAME"", ""ChainId"")
SELECT {id}, '{name}', ""ID""
FROM ""Chains""
WHERE ""NAME"" = 'main'
  AND NOT EXISTS (
      SELECT 1 FROM ""EventKinds"" WHERE ""NAME"" = '{name}'
);
");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
