using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Main.Migrations
{
    /// <inheritdoc />
    public partial class Update34_NormalizeNumericEventKindNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normalize legacy numeric EventKinds.NAME values (for example "64") to canonical
            // symbolic names (for example "ContractKill") without any chain resync.
            // If canonical row already exists in the same chain, remap Events.EventKindId and
            // remove duplicate numeric row; otherwise rename numeric row in place.
            migrationBuilder.Sql(@"
WITH event_kind_map AS (
    SELECT * FROM (VALUES
        ('1',  'ValueCreate'),
        ('2',  'TokenMint'),
        ('3',  'TokenStake'),
        ('4',  'OrganizationAdd'),
        ('5',  'ValidatorElect'),
        ('6',  'GasEscrow'),
        ('7',  'GasPayment'),
        ('8',  'TokenClaim'),
        ('9',  'TokenBurn'),
        ('10', 'CrownRewards'),
        ('11', 'TokenSend'),
        ('12', 'TokenReceive'),
        ('13', 'ContractUpgrade'),
        ('14', 'ExecutionFailure'),
        ('15', 'OrganizationRemove'),
        ('16', 'AddressUnregister'),
        ('17', 'AddressRegister'),
        ('18', 'Infusion'),
        ('19', 'OrderFilled'),
        ('20', 'AddressMigration'),
        ('21', 'OrderCreated'),
        ('22', 'OrderCancelled'),
        ('23', 'OrderBid'),
        ('24', 'Custom'),
        ('25', 'PollCreated'),
        ('26', 'PollVote'),
        ('27', 'PollClosed'),
        ('28', 'ValueUpdate'),
        ('29', 'MasterClaim'),
        ('30', 'Inflation'),
        ('31', 'TokenCreate'),
        ('32', 'FileCreate'),
        ('33', 'FileDelete'),
        ('34', 'ValidatorRemove'),
        ('35', 'Log'),
        ('36', 'ContractDeploy'),
        ('37', 'Unknown'),
        ('38', 'ChainCreate'),
        ('39', 'AddressLink'),
        ('40', 'AddressUnlink'),
        ('41', 'OrganizationCreate'),
        ('42', 'OrderClosed'),
        ('43', 'FeedCreate'),
        ('44', 'FeedUpdate'),
        ('45', 'ValidatorPropose'),
        ('46', 'ValidatorSwitch'),
        ('47', 'PackedNFT'),
        ('48', 'ChannelCreate'),
        ('49', 'ChannelRefill'),
        ('50', 'ChannelSettle'),
        ('51', 'LeaderboardCreate'),
        ('52', 'LeaderboardInsert'),
        ('53', 'LeaderboardReset'),
        ('54', 'PlatformCreate'),
        ('55', 'ChainSwap'),
        ('56', 'ContractRegister'),
        ('57', 'OwnerAdded'),
        ('58', 'OwnerRemoved'),
        ('59', 'DomainCreate'),
        ('60', 'DomainDelete'),
        ('61', 'TaskStart'),
        ('62', 'TaskStop'),
        ('63', 'Crowdsale'),
        ('64', 'ContractKill'),
        ('65', 'OrganizationKill'),
        ('66', 'Custom_V2'),
        ('67', 'GovernanceSetGasConfig'),
        ('68', 'GovernanceSetChainConfig')
    ) AS map(numeric_name, canonical_name)
),
pairs AS (
    SELECT
        bad.""ID"" AS bad_id,
        bad.""ChainId"" AS chain_id,
        map.canonical_name,
        canonical.""ID"" AS canonical_id
    FROM ""EventKinds"" bad
    JOIN event_kind_map map
      ON bad.""NAME"" = map.numeric_name
    LEFT JOIN ""EventKinds"" canonical
      ON canonical.""ChainId"" = bad.""ChainId""
     AND canonical.""NAME"" = map.canonical_name
)
UPDATE ""Events"" e
SET ""EventKindId"" = p.canonical_id
FROM pairs p
WHERE p.canonical_id IS NOT NULL
  AND e.""EventKindId"" = p.bad_id;
");

            migrationBuilder.Sql(@"
WITH event_kind_map AS (
    SELECT * FROM (VALUES
        ('1',  'ValueCreate'),
        ('2',  'TokenMint'),
        ('3',  'TokenStake'),
        ('4',  'OrganizationAdd'),
        ('5',  'ValidatorElect'),
        ('6',  'GasEscrow'),
        ('7',  'GasPayment'),
        ('8',  'TokenClaim'),
        ('9',  'TokenBurn'),
        ('10', 'CrownRewards'),
        ('11', 'TokenSend'),
        ('12', 'TokenReceive'),
        ('13', 'ContractUpgrade'),
        ('14', 'ExecutionFailure'),
        ('15', 'OrganizationRemove'),
        ('16', 'AddressUnregister'),
        ('17', 'AddressRegister'),
        ('18', 'Infusion'),
        ('19', 'OrderFilled'),
        ('20', 'AddressMigration'),
        ('21', 'OrderCreated'),
        ('22', 'OrderCancelled'),
        ('23', 'OrderBid'),
        ('24', 'Custom'),
        ('25', 'PollCreated'),
        ('26', 'PollVote'),
        ('27', 'PollClosed'),
        ('28', 'ValueUpdate'),
        ('29', 'MasterClaim'),
        ('30', 'Inflation'),
        ('31', 'TokenCreate'),
        ('32', 'FileCreate'),
        ('33', 'FileDelete'),
        ('34', 'ValidatorRemove'),
        ('35', 'Log'),
        ('36', 'ContractDeploy'),
        ('37', 'Unknown'),
        ('38', 'ChainCreate'),
        ('39', 'AddressLink'),
        ('40', 'AddressUnlink'),
        ('41', 'OrganizationCreate'),
        ('42', 'OrderClosed'),
        ('43', 'FeedCreate'),
        ('44', 'FeedUpdate'),
        ('45', 'ValidatorPropose'),
        ('46', 'ValidatorSwitch'),
        ('47', 'PackedNFT'),
        ('48', 'ChannelCreate'),
        ('49', 'ChannelRefill'),
        ('50', 'ChannelSettle'),
        ('51', 'LeaderboardCreate'),
        ('52', 'LeaderboardInsert'),
        ('53', 'LeaderboardReset'),
        ('54', 'PlatformCreate'),
        ('55', 'ChainSwap'),
        ('56', 'ContractRegister'),
        ('57', 'OwnerAdded'),
        ('58', 'OwnerRemoved'),
        ('59', 'DomainCreate'),
        ('60', 'DomainDelete'),
        ('61', 'TaskStart'),
        ('62', 'TaskStop'),
        ('63', 'Crowdsale'),
        ('64', 'ContractKill'),
        ('65', 'OrganizationKill'),
        ('66', 'Custom_V2'),
        ('67', 'GovernanceSetGasConfig'),
        ('68', 'GovernanceSetChainConfig')
    ) AS map(numeric_name, canonical_name)
),
pairs AS (
    SELECT
        bad.""ID"" AS bad_id,
        bad.""ChainId"" AS chain_id,
        map.canonical_name,
        canonical.""ID"" AS canonical_id
    FROM ""EventKinds"" bad
    JOIN event_kind_map map
      ON bad.""NAME"" = map.numeric_name
    LEFT JOIN ""EventKinds"" canonical
      ON canonical.""ChainId"" = bad.""ChainId""
     AND canonical.""NAME"" = map.canonical_name
)
DELETE FROM ""EventKinds"" ek
USING pairs p
WHERE p.canonical_id IS NOT NULL
  AND ek.""ID"" = p.bad_id;
");

            migrationBuilder.Sql(@"
WITH event_kind_map AS (
    SELECT * FROM (VALUES
        ('1',  'ValueCreate'),
        ('2',  'TokenMint'),
        ('3',  'TokenStake'),
        ('4',  'OrganizationAdd'),
        ('5',  'ValidatorElect'),
        ('6',  'GasEscrow'),
        ('7',  'GasPayment'),
        ('8',  'TokenClaim'),
        ('9',  'TokenBurn'),
        ('10', 'CrownRewards'),
        ('11', 'TokenSend'),
        ('12', 'TokenReceive'),
        ('13', 'ContractUpgrade'),
        ('14', 'ExecutionFailure'),
        ('15', 'OrganizationRemove'),
        ('16', 'AddressUnregister'),
        ('17', 'AddressRegister'),
        ('18', 'Infusion'),
        ('19', 'OrderFilled'),
        ('20', 'AddressMigration'),
        ('21', 'OrderCreated'),
        ('22', 'OrderCancelled'),
        ('23', 'OrderBid'),
        ('24', 'Custom'),
        ('25', 'PollCreated'),
        ('26', 'PollVote'),
        ('27', 'PollClosed'),
        ('28', 'ValueUpdate'),
        ('29', 'MasterClaim'),
        ('30', 'Inflation'),
        ('31', 'TokenCreate'),
        ('32', 'FileCreate'),
        ('33', 'FileDelete'),
        ('34', 'ValidatorRemove'),
        ('35', 'Log'),
        ('36', 'ContractDeploy'),
        ('37', 'Unknown'),
        ('38', 'ChainCreate'),
        ('39', 'AddressLink'),
        ('40', 'AddressUnlink'),
        ('41', 'OrganizationCreate'),
        ('42', 'OrderClosed'),
        ('43', 'FeedCreate'),
        ('44', 'FeedUpdate'),
        ('45', 'ValidatorPropose'),
        ('46', 'ValidatorSwitch'),
        ('47', 'PackedNFT'),
        ('48', 'ChannelCreate'),
        ('49', 'ChannelRefill'),
        ('50', 'ChannelSettle'),
        ('51', 'LeaderboardCreate'),
        ('52', 'LeaderboardInsert'),
        ('53', 'LeaderboardReset'),
        ('54', 'PlatformCreate'),
        ('55', 'ChainSwap'),
        ('56', 'ContractRegister'),
        ('57', 'OwnerAdded'),
        ('58', 'OwnerRemoved'),
        ('59', 'DomainCreate'),
        ('60', 'DomainDelete'),
        ('61', 'TaskStart'),
        ('62', 'TaskStop'),
        ('63', 'Crowdsale'),
        ('64', 'ContractKill'),
        ('65', 'OrganizationKill'),
        ('66', 'Custom_V2'),
        ('67', 'GovernanceSetGasConfig'),
        ('68', 'GovernanceSetChainConfig')
    ) AS map(numeric_name, canonical_name)
),
pairs AS (
    SELECT
        bad.""ID"" AS bad_id,
        map.canonical_name,
        canonical.""ID"" AS canonical_id
    FROM ""EventKinds"" bad
    JOIN event_kind_map map
      ON bad.""NAME"" = map.numeric_name
    LEFT JOIN ""EventKinds"" canonical
      ON canonical.""ChainId"" = bad.""ChainId""
     AND canonical.""NAME"" = map.canonical_name
)
UPDATE ""EventKinds"" ek
SET ""NAME"" = p.canonical_name
FROM pairs p
WHERE p.canonical_id IS NULL
  AND ek.""ID"" = p.bad_id;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data backfill is intentionally forward-only.
        }
    }
}
