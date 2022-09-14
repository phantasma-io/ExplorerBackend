#nullable enable
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Backend.Service.Api;

//since we got different naming in the API
// ReSharper disable InconsistentNaming
/// <summary>
///     Ntf Metadata Information
/// </summary>
public class NftMetadata
{
    /// <summary>
    ///     Description of the NFT
    /// </summary>
    public string? description { get; set; }

    /// <summary>
    ///     Name of the NFT
    /// </summary>
    public string? name { get; set; }

    /// <summary>
    ///     URL to the Image
    /// </summary>
    public string? image { get; set; }

    /// <summary>
    ///     URL to the Video
    /// </summary>
    public string? video { get; set; }

    /// <summary>
    ///     Url to the Information of the Image/Video
    /// </summary>
    public string? info_url { get; set; }

    /// <summary>
    ///     Data written into ROM
    /// </summary>
    public string? rom { get; set; }

    /// <summary>
    ///     Data written into RAM
    /// </summary>
    public string? ram { get; set; }

    /// <summary>
    ///     Date when NTF was minted in UTC unixseconds
    /// </summary>
    public string? mint_date { get; set; }

    /// <summary>
    ///     Number of the minted NFT
    /// </summary>
    public string? mint_number { get; set; }
}

/// <summary>
///     Ntf Infused Information
/// </summary>
public class InfusedInto
{
    /// <summary>
    ///     TokenID of the Infusion
    /// </summary>
    public string? token_id { get; set; }

    /// <summary>
    ///     Name of the chain
    /// </summary>
    /// <example>main</example>
    public string? chain { get; set; }

    /// <summary>
    ///     Contract Information with Hash, Name and Symbol
    /// </summary>
    public Contract? contract { get; set; }
}

/// <summary>
///     Ntf Ownership
/// </summary>
public class NftOwnershipResult
{
    /// <summary>
    ///     Address of the NTF Owner
    /// </summary>
    public string? address { get; set; }

    /// <summary>
    ///     Address Name of the NTF Owner
    /// </summary>
    public string? onchain_name { get; set; }

    /// <summary>
    ///     UserName of the NTF Owner
    /// </summary>
    public string? offchain_name { get; set; }

    /// <summary>
    ///     Amount how much the address owns of the NTF
    /// </summary>
    /// <example>1</example>
    public int amount { get; set; }
}

/// <summary>
///     Series Info
/// </summary>
public class Series
{
    /// <summary>
    ///     Internal ID, no Information value
    /// </summary>
    public int id { get; set; }

    /// <summary>
    ///     Series ID
    /// </summary>
    /// <example>0</example>
    public string? series_id { get; set; }

    /// <summary>
    ///     Address of the creator
    /// </summary>
    public string? creator { get; set; }

    /// <summary>
    ///     current available supply
    /// </summary>
    /// <example>2</example>
    public int current_supply { get; set; }

    /// <summary>
    ///     maximum supply, 0 = infinite
    /// </summary>
    /// <example>52</example>
    public int max_supply { get; set; }

    /// <summary>
    ///     Mode of the Series
    /// </summary>
    /// <example>Duplicated</example>
    /// <example>Unique</example>
    public string? mode_name { get; set; }

    /// <summary>
    ///     Name of the Series
    /// </summary>
    public string? name { get; set; }

    /// <summary>
    ///     Description of the Series
    /// </summary>
    public string? description { get; set; }

    /// <summary>
    ///     Type of the Series
    /// </summary>
    public string? image { get; set; }

    /// <summary>
    ///     Royalties of the Series
    /// </summary>
    /// <example>0</example>
    public string? royalties { get; set; }

    /// <summary>
    ///     Type of the Series
    /// </summary>
    /// <example>20</example>
    public int type { get; set; }

    /// <summary>
    ///     Attribute 1 Type
    /// </summary>
    /// <example>Artist Name</example>
    public string? attr_type_1 { get; set; }

    /// <summary>
    ///     Attribute 1 value
    /// </summary>
    /// <example>Peter Painter</example>
    public string? attr_value_1 { get; set; }

    /// <summary>
    ///     Attribute 2 Type
    /// </summary>
    /// <example>Location</example>
    public string? attr_type_2 { get; set; }

    /// <summary>
    ///     Attribute 2 value
    /// </summary>
    /// <example>Greenland</example>
    public string? attr_value_2 { get; set; }

    /// <summary>
    ///     Attribute 3 Type
    /// </summary>
    /// <example>Size</example>
    public string? attr_type_3 { get; set; }

    /// <summary>
    ///     Attribute 3 value
    /// </summary>
    /// <example>1920x1080</example>
    public string? attr_value_3 { get; set; }
}

/// <summary>
///     Infusion info of a NTF
/// </summary>
public class Infusion
{
    /// <summary>
    ///     Symbol of the Infusion
    /// </summary>
    /// <example>KCAL</example>
    public string? key { get; set; }

    /// <summary>
    ///     Amount used for the Infusion
    /// </summary>
    /// <example>100</example>
    public string? value { get; set; }
}

/// <summary>
///     Base Information from the NFT
/// </summary>
public class Nft
{
    /// <summary>
    ///     TokenID of the Infusion
    /// </summary>
    public string? token_id { get; set; }

    /// <summary>
    ///     Name of the chain
    /// </summary>
    /// <example>main</example>
    public string? chain { get; set; }

    /// <summary>
    ///     Symbol of the NFT
    /// </summary>
    /// <example>KCAL</example>
    public string? symbol { get; set; }

    /// <summary>
    ///     Creator Address of the NFT
    /// </summary>
    public string? creator_address { get; set; }

    /// <summary>
    ///     Creator Address Name of the NFT
    /// </summary>
    public string? creator_onchain_name { get; set; }

    /// <summary>
    ///     List of the Ownerships
    /// </summary>
    public NftOwnershipResult[]? owners { get; set; }

    /// <summary>
    ///     Contract Information with Hash, Name and Symbol
    /// </summary>
    public Contract? contract { get; set; }

    /// <summary>
    ///     Metadata Information
    /// </summary>
    public NftMetadata? nft_metadata { get; set; }

    /// <summary>
    ///     Linked Series
    /// </summary>
    public Series? series { get; set; }

    /// <summary>
    ///     List of Infusions
    /// </summary>
    public Infusion[]? infusion { get; set; }

    /// <summary>
    ///     Information what it is infused into
    /// </summary>
    public InfusedInto? infused_into { get; set; }
}

/// <summary>
///     Result List of NFTs
/// </summary>
public class NftsResult
{
    /// <summary>
    ///     Total number of found assets
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of available assets
    /// </summary>
    public Nft[]? nfts { get; set; }
}

/// <summary>
///     Base Information of an Event
/// </summary>
public class Event
{
    /// <summary>
    ///     Internal ID, no Information value
    /// </summary>
    public int event_id { get; set; }

    /// <summary>
    ///     Name of the chain
    /// </summary>
    /// <example>main</example>
    public string? chain { get; set; }

    /// <summary>
    ///     Date in UTC unixseconds
    /// </summary>
    public string? date { get; set; }

    /// <summary>
    ///     hash of the corresponding block
    /// </summary>
    public string? block_hash { get; set; }

    /// <summary>
    ///     hash of the corresponding transaction
    /// </summary>
    public string? transaction_hash { get; set; }

    /// <summary>
    ///     TokenID
    /// </summary>
    public string? token_id { get; set; }

    /// <summary>
    ///     Kind of the Event, Valid values at eventkinds
    /// </summary>
    public string? event_kind { get; set; }

    /// <summary>
    ///     Address of the Event
    /// </summary>
    public string? address { get; set; }

    /// <summary>
    ///     Address Name the Event
    /// </summary>
    public string? address_name { get; set; }

    /// <summary>
    ///     Contract Information with Hash, Name and Symbol
    /// </summary>
    public Contract? contract { get; set; }

    /// <summary>
    ///     Ntf Metadata of the Processed NFT
    /// </summary>
    public NftMetadata? nft_metadata { get; set; }

    /// <summary>
    ///     Series of the Processed NFT
    /// </summary>
    public Series? series { get; set; }

    /// <summary>
    ///     EventKinds ValidatorElect or ValidatorPropose
    /// </summary>
    public AddressEvent? address_event { get; set; }

    /// <summary>
    ///     EventKinds ValueCreate or ValueUpdate
    /// </summary>
    public ChainEvent? chain_event { get; set; }

    /// <summary>
    ///     EventKinds GasEscrow or GasPayment
    /// </summary>
    public GasEvent? gas_event { get; set; }

    /// <summary>
    ///     EventKinds FileCreate or FileDelete
    /// </summary>
    public HashEvent? hash_event { get; set; }

    /// <summary>
    ///     EventKinds Infusion
    /// </summary>
    public InfusionEvent? infusion_event { get; set; }

    /// <summary>
    ///     EventKinds OrderCancelled, OrderClosed, OrderCreated, OrderFilled or OrderBid
    /// </summary>
    public MarketEvent? market_event { get; set; }

    /// <summary>
    ///     EventKinds OrganizationAdd or OrganizationRemove
    /// </summary>
    public OrganizationEvent? organization_event { get; set; }

    /// <summary>
    ///     EventKinds Crowdsale
    /// </summary>
    public SaleEvent? sale_event { get; set; }

    /// <summary>
    ///     EventKinds ChainCreate, TokenCreate, ContractUpgrade, AddressRegister, ContractDeploy, PlatformCreate,
    ///     OrganizationCreate, Log or AddressUnregister
    /// </summary>
    public StringEvent? string_event { get; set; }

    /// <summary>
    ///     EventKinds TokenMint, TokenClaim, TokenBurn, TokenSend, TokenReceive, TokenStake, CrownRewards or Inflation
    /// </summary>
    public TokenEvent? token_event { get; set; }

    /// <summary>
    ///     EventKinds ChainSwap
    /// </summary>
    public TransactionSettleEvent? transaction_settle_event { get; set; }
}

/// <summary>
///     Result Events
/// </summary>
public class EventsResult
{
    /// <summary>
    ///     Total number of found events
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of available events
    /// </summary>
    public Event[]? events { get; set; }
}

/// <summary>
///     Result Series
/// </summary>
public class SeriesResult
{
    /// <summary>
    ///     Total number of found series
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of available series
    /// </summary>
    public Series[]? series { get; set; }
}

/// <summary>
///     Chain Information
/// </summary>
public class Chain
{
    /// <summary>
    ///     Returns the chain name
    /// </summary>
    public string? chain_name { get; set; }

    /// <summary>
    ///     Returns the chain height
    /// </summary>
    public string? chain_height { get; set; }
}

/// <summary>
///     Result Chain
/// </summary>
public class ChainResult
{
    /// <summary>
    ///     total number of found chains
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of available chains
    /// </summary>
    public Chain[]? chains { get; set; }
}

/// <summary>
///     Event Kind Element
/// </summary>
public class EventKind
{
    /// <summary>
    ///     Returns the kind name
    /// </summary>
    public string? name { get; set; }
}

/// <summary>
///     Event Kind Result
/// </summary>
public class EventKindResult
{
    /// <summary>
    ///     Total number of found eventKinds
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of available eventKinds
    /// </summary>
    public EventKind[]? event_kinds { get; set; }
}

/// <summary>
///     Basic Address Information
/// </summary>
public class Address
{
    /// <summary>
    ///     Returns the address
    /// </summary>
    public string? address { get; set; }

    /// <summary>
    ///     Returns the address name
    /// </summary>
    public string? address_name { get; set; }

    /// <summary>
    ///     Returns the validator kind name
    /// </summary>
    public string? validator_kind { get; set; }

    /// <summary>
    ///     Returns the stake value
    /// </summary>
    public string? stake { get; set; }

    /// <summary>
    ///     Returns the unclaimed value
    /// </summary>
    public string? unclaimed { get; set; }

    /// <summary>
    ///     Returns the relay value
    /// </summary>
    public string? relay { get; set; }

    /// <summary>
    ///     Returns the address storage Information
    /// </summary>
    public AddressStorage? storage { get; set; }

    /// <summary>
    ///     Returns the address stakes Information
    /// </summary>
    public AddressStakes? stakes { get; set; }

    /// <summary>
    ///     Returns the address balances Information
    /// </summary>
    public AddressBalance[]? balances { get; set; }
}

/// <summary>
///     Address Result
/// </summary>
public class AddressResult
{
    /// <summary>
    ///     Total number of found addresses
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of available addresses
    /// </summary>
    public Address[]? addresses { get; set; }
}

/// <summary>
///     Token Base Information
/// </summary>
public class Token
{
    /// <summary>
    ///     Returns Symbol
    /// </summary>
    /// <example>SOUL</example>
    public string? symbol { get; set; }

    /// <summary>
    ///     Returns fungible value of the token
    /// </summary>
    public bool fungible { get; set; }

    /// <summary>
    ///     Returns transferable value of the token
    /// </summary>
    public bool transferable { get; set; }

    /// <summary>
    ///     Returns finite value of the token
    /// </summary>
    public bool finite { get; set; }

    /// <summary>
    ///     Returns divisible value of the token
    /// </summary>
    public bool divisible { get; set; }

    /// <summary>
    ///     Returns fuel value of the token
    /// </summary>
    public bool fuel { get; set; }

    /// <summary>
    ///     Returns stakable value of the token
    /// </summary>
    public bool stakable { get; set; }

    /// <summary>
    ///     Returns fiat value of the token
    /// </summary>
    public bool fiat { get; set; }

    /// <summary>
    ///     Returns swappable value of the token
    /// </summary>
    public bool swappable { get; set; }

    /// <summary>
    ///     Returns burnable value of the token
    /// </summary>
    public bool burnable { get; set; }

    /// <summary>
    ///     Returns decimal value of the token
    /// </summary>
    /// <example>8</example>
    public int decimals { get; set; }

    /// <summary>
    ///     Returns the current supply of the token
    /// </summary>
    public string? current_supply { get; set; }

    /// <summary>
    ///     Returns the max supply of the token, 0 = infinite
    /// </summary>
    public string? max_supply { get; set; }

    /// <summary>
    ///     Returns the burned supply of the token
    /// </summary>
    public string? burned_supply { get; set; }

    /// <summary>
    ///     Returns the script of the token, raw
    /// </summary>
    public string? script_raw { get; set; }

    /// <summary>
    ///     Returns currency price information
    /// </summary>
    public Price? price { get; set; }

    /// <summary>
    ///     Event of the creation of the token
    /// </summary>
    public Event? create_event { get; set; }

    /// <summary>
    ///     Logos of the token
    /// </summary>
    public TokenLogo[]? token_logos { get; set; }
}

/// <summary>
///     Token Result
/// </summary>
public class TokenResult
{
    /// <summary>
    ///     Total number of found tokens
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of available tokens
    /// </summary>
    public Token[]? tokens { get; set; }
}

/// <summary>
///     Transaction Base Information
/// </summary>
public class Transaction
{
    /// <summary>
    ///     Hash of the transaction
    /// </summary>
    public string? hash { get; set; }

    /// <summary>
    ///     Hash of the corresponding block
    /// </summary>
    public string? block_hash { get; set; }

    /// <summary>
    ///     Height of the Block from the transaction
    /// </summary>
    public string? block_height { get; set; }

    /// <summary>
    ///     index in the Block from the transaction
    /// </summary>
    public int index { get; set; }

    /// <summary>
    ///     unixseconds timestamp in UTC of the transaction
    /// </summary>
    public string? date { get; set; }

    /// <summary>
    ///     fee for the transaction in kcal
    /// </summary>
    public string? fee { get; set; }

    /// <summary>
    ///     script of the contract, raw
    /// </summary>
    public string? script_raw { get; set; }

    /// <summary>
    ///     result of the transaction
    /// </summary>
    public string? result { get; set; }

    /// <summary>
    ///     payload of the transaction
    /// </summary>
    public string? payload { get; set; }

    /// <summary>
    ///     when the transaction will expire in UTC of the transaction
    /// </summary>
    public string? expiration { get; set; }

    /// <summary>
    ///     List of Events from the transaction
    /// </summary>
    public Event[]? events { get; set; }
}

/// <summary>
///     Transaction Result
/// </summary>
public class TransactionResult
{
    /// <summary>
    ///     Total number of found transactions
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of available transactions
    /// </summary>
    public Transaction[]? transactions { get; set; }
}

/// <summary>
///     Contract Base Information
/// </summary>
public class Contract
{
    /// <summary>
    ///     Name of the contract
    /// </summary>
    /// <example>SOUL</example>
    public string? name { get; set; }

    /// <summary>
    ///     Hash of the contract
    /// </summary>
    /// <example>SOUL</example>
    public string? hash { get; set; }

    /// <summary>
    ///     Symbol of the contract
    /// </summary>
    /// <example>SOUL</example>
    public string? symbol { get; set; }

    /// <summary>
    ///     address of the contract
    /// </summary>
    public Address? address { get; set; }

    /// <summary>
    ///     script of the contract, raw
    /// </summary>
    public string? script_raw { get; set; }

    /// <summary>
    ///     token of the contract
    /// </summary>
    public Token? token { get; set; }

    /// <summary>
    ///     methods of the contract
    /// </summary>
    public JsonElement? methods { get; set; }

    /// <summary>
    ///     Event of the creation of the contract
    /// </summary>
    public Event? create_event { get; set; }
}

/// <summary>
///     Contract Result
/// </summary>
public class ContractResult
{
    /// <summary>
    ///     Total number of found contracts
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of available contracts
    /// </summary>
    public Contract[]? contracts { get; set; }
}

/// <summary>
///     Instruction of a Script
/// </summary>
public class Instruction
{
    /// <summary>
    ///     Instruction of a Script
    /// </summary>
    public string? instruction { get; set; }
}

/// <summary>
///     Script
/// </summary>
public class Script
{
    /// <summary>
    ///     Script Raw
    /// </summary>
    [Required]
    public string? script_raw { get; set; }
}

/// <summary>
///     Disassembled Instructions of a Script
/// </summary>
public class DisassemblerResult
{
    /// <summary>
    ///     Total number of Instructions of the parsed Script
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List instructions of the parsed Script
    /// </summary>
    public Instruction[]? Instructions { get; set; }
}

/// <summary>
///     Base Information of an Organization
/// </summary>
public class Organization
{
    /// <summary>
    ///     ID/Name of the organization
    /// </summary>
    public string? id { get; set; }

    /// <summary>
    ///     Full Name of the organization
    /// </summary>
    public string? name { get; set; }

    /// <summary>
    ///     Size of the Member List
    /// </summary>
    public long? size { get; set; }

    /// <summary>
    ///     Event of the creation of the organization
    /// </summary>
    public Event? create_event { get; set; }

    /// <summary>
    ///     Address of the organization
    /// </summary>
    public Address? address { get; set; }
}

/// <summary>
///     Organization Result
/// </summary>
public class OrganizationResult
{
    /// <summary>
    ///     Total number of organizations
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of available organizations
    /// </summary>
    public Organization[]? organizations { get; set; }
}

/// <summary>
///     Base Information of an Block
/// </summary>
public class Block
{
    /// <summary>
    ///     height of the block
    /// </summary>
    public string? height { get; set; }

    /// <summary>
    ///     hash of this block
    /// </summary>
    public string? hash { get; set; }

    /// <summary>
    ///     hash of the previous block
    /// </summary>
    public string? previous_hash { get; set; }

    /// <summary>
    ///     used protocol version
    /// </summary>
    public int protocol { get; set; }

    /// <summary>
    ///     chain address
    /// </summary>
    public string? chain_address { get; set; }

    /// <summary>
    ///     validator address
    /// </summary>
    public string? validator_address { get; set; }

    /// <summary>
    ///     unixseconds timestamp in UTC of the block
    /// </summary>
    public string? date { get; set; }

    /// <summary>
    ///     reward of the block
    /// </summary>
    public string? reward { get; set; }

    /// <summary>
    ///     list of transactions
    /// </summary>
    public Transaction[]? transactions { get; set; }
}

/// <summary>
///     Block Result
/// </summary>
public class BlockResult
{
    /// <summary>
    ///     Total number of found Blocks
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of blocks
    /// </summary>
    public Block[]? blocks { get; set; }
}

/// <summary>
///     Base Information of an Oracle
/// </summary>
public class Oracle
{
    /// <summary>
    ///     url of the oracle
    /// </summary>
    public string? url { get; set; }

    /// <summary>
    ///     content of the oracle
    /// </summary>
    public string? content { get; set; }
}

/// <summary>
///     Oracle Result
/// </summary>
public class OracleResult
{
    /// <summary>
    ///     Total number of found Oracles for Block
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of Oracles
    /// </summary>
    public Oracle[]? oracles { get; set; }
}

/// <summary>
///     Base Information of an External
/// </summary>
public class External
{
    /// <summary>
    ///     token information
    /// </summary>
    public Token? token { get; set; }

    /// <summary>
    ///     hash
    /// </summary>
    public string? hash { get; set; }
}

/// <summary>
///     Base Information of a PlatformInterop
/// </summary>
public class PlatformInterop
{
    /// <summary>
    ///     address from our system
    /// </summary>
    public Address? local_address { get; set; }

    /// <summary>
    ///     address on the platform
    /// </summary>
    public string? external_address { get; set; }
}

/// <summary>
///     Base Information of a PlatformToken
/// </summary>
public class PlatformToken
{
    /// <summary>
    ///     token name on the platform
    /// </summary>
    public string? name { get; set; }
}

/// <summary>
///     Base Information of a Platform
/// </summary>
public class Platform
{
    /// <summary>
    ///     name of the platform
    /// </summary>
    public string? name { get; set; }

    /// <summary>
    ///     chain hash
    /// </summary>
    public string? chain { get; set; }

    /// <summary>
    ///     fuel currency
    /// </summary>
    public string? fuel { get; set; }

    /// <summary>
    ///     list from our tokens on their system
    /// </summary>
    public External[]? externals { get; set; }

    /// <summary>
    ///     local to external address
    /// </summary>
    public PlatformInterop[]? platform_interops { get; set; }

    /// <summary>
    ///     tokens on their system
    /// </summary>
    public PlatformToken[]? platform_tokens { get; set; }

    /// <summary>
    ///     Event of the creation of the platform
    /// </summary>
    public Event? create_event { get; set; }
}

/// <summary>
///     Platform Result
/// </summary>
public class PlatformResult
{
    /// <summary>
    ///     Total number of platforms
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     platforms known on the backend
    /// </summary>
    public Platform[]? platforms { get; set; }
}

/// <summary>
///     EventKinds ValidatorElect or ValidatorPropose
/// </summary>
public class AddressEvent
{
    /// <summary>
    ///     address of the event
    /// </summary>
    public Address? address { get; set; }
}

/// <summary>
///     EventKinds ValueCreate or ValueUpdate
/// </summary>
public class ChainEvent
{
    /// <summary>
    ///     name of the chain event data
    /// </summary>
    public string? name { get; set; }

    /// <summary>
    ///     value of the chain event data
    /// </summary>
    public string? value { get; set; }

    /// <summary>
    ///     chain of the chain event data
    /// </summary>
    public Chain? chain { get; set; }
}

/// <summary>
///     EventKinds GasEscrow or GasPayment
/// </summary>
public class GasEvent
{
    /// <summary>
    ///     price of the gas event data
    /// </summary>
    public string? price { get; set; }

    /// <summary>
    ///     amount of the gas event data
    /// </summary>
    public string? amount { get; set; }

    /// <summary>
    ///     address of the gas event data
    /// </summary>
    public Address? address { get; set; }
}

/// <summary>
///     EventKinds FileCreate or FileDelete
/// </summary>
public class HashEvent
{
    /// <summary>
    ///     hash of the event
    /// </summary>
    public string? hash { get; set; }
}

/// <summary>
///     EventKinds Infusion
/// </summary>
public class InfusionEvent
{
    /// <summary>
    ///     string of the token_id
    /// </summary>
    public string? token_id { get; set; }

    /// <summary>
    ///     base token info
    /// </summary>
    public Token? base_token { get; set; }

    /// <summary>
    ///     infused token info
    /// </summary>
    public Token? infused_token { get; set; }

    /// <summary>
    ///     infused value
    /// </summary>
    public string? infused_value { get; set; }
}

/// <summary>
///     EventKinds OrderCancelled, OrderClosed, OrderCreated, OrderFilled or OrderBid
/// </summary>
public class MarketEvent
{
    /// <summary>
    ///     base token
    /// </summary>
    public Token? base_token { get; set; }

    /// <summary>
    ///     quote token
    /// </summary>
    public Token? quote_token { get; set; }

    /// <summary>
    ///     Kind of the Sale; Fixed, Classic, Reserve or Dutch
    /// </summary>
    public string? market_event_kind { get; set; }

    /// <summary>
    ///     Market ID
    /// </summary>
    public string? market_id { get; set; }

    /// <summary>
    ///     Price of the Sale
    /// </summary>
    public string? price { get; set; }

    /// <summary>
    ///     Final Price of the Sale
    /// </summary>
    public string? end_price { get; set; }

    /// <summary>
    ///     Fiat Price
    /// </summary>
    public FiatPrice? fiat_price { get; set; }
}

/// <summary>
///     EventKinds OrganizationAdd or OrganizationRemove
/// </summary>
public class OrganizationEvent
{
    /// <summary>
    ///     Organization
    /// </summary>
    public Organization? organization { get; set; }

    /// <summary>
    ///     Address
    /// </summary>
    public Address? address { get; set; }
}

/// <summary>
///     EventKinds Crowdsale
/// </summary>
public class SaleEvent
{
    /// <summary>
    ///     Hash of the Sale
    /// </summary>
    public string? hash { get; set; }

    /// <summary>
    ///     Kind of the Sale; Creation
    /// </summary>
    public string? sale_event_kind { get; set; }
}

/// <summary>
///     EventKinds ChainCreate, TokenCreate, ContractUpgrade, AddressRegister, ContractDeploy, PlatformCreate,
///     OrganizationCreate, Log or AddressUnregister
/// </summary>
public class StringEvent
{
    /// <summary>
    ///     String Content of the Event
    /// </summary>
    public string? string_value { get; set; }
}

/// <summary>
///     EventKinds TokenMint, TokenClaim, TokenBurn, TokenSend, TokenReceive, TokenStake, CrownRewards or Inflation
/// </summary>
public class TokenEvent
{
    /// <summary>
    ///     Token
    /// </summary>
    public Token? token { get; set; }

    /// <summary>
    ///     Value of the Event
    /// </summary>
    public string? value { get; set; }

    /// <summary>
    ///     Chain name of the Event
    /// </summary>
    /// <example>main</example>
    public string? chain_name { get; set; }
}

/// <summary>
///     EventKinds ChainSwap
/// </summary>
public class TransactionSettleEvent
{
    /// <summary>
    ///     Hash from the Transaction
    /// </summary>
    public string? hash { get; set; }

    /// <summary>
    ///     Target Platform, can also be phantasma
    /// </summary>
    public Platform? platform { get; set; }
}

/// <summary>
///     Base Information of a FiatPrice
/// </summary>
public class FiatPrice
{
    /// <summary>
    ///     Fiat Currency
    /// </summary>
    /// <example>USD</example>
    public string? fiat_currency { get; set; }

    /// <summary>
    ///     Fiat Price
    /// </summary>
    public string? fiat_price { get; set; }

    /// <summary>
    ///     Fiat End Price
    /// </summary>
    public string? fiat_price_end { get; set; }
}

/// <summary>
///     Base Information of a Price
/// </summary>
public class Price
{
    /// <summary>
    ///     US Dollar
    /// </summary>
    public decimal? usd { get; set; }

    /// <summary>
    ///     Euro
    /// </summary>
    public decimal? eur { get; set; }

    /// <summary>
    ///     Pound Sterling
    /// </summary>
    public decimal? gbp { get; set; }

    /// <summary>
    ///     Japanese Yen
    /// </summary>
    public decimal? jpy { get; set; }

    /// <summary>
    ///     Canadian Dollar
    /// </summary>
    public decimal? cad { get; set; }

    /// <summary>
    ///     Australian Dollar
    /// </summary>
    public decimal? aud { get; set; }

    /// <summary>
    ///     Chinese Yuan
    /// </summary>
    public decimal? cny { get; set; }

    /// <summary>
    ///     Russian Rubel
    /// </summary>
    public decimal? rub { get; set; }
}

/// <summary>
///     Base Information of a HistoryPrice
/// </summary>
public class HistoryPrice
{
    /// <summary>
    ///     Returns Symbol
    /// </summary>
    /// <example>SOUL</example>
    public string? symbol { get; set; }

    /// <summary>
    ///     Information about the Token
    /// </summary>
    public Token? token { get; set; }

    /// <summary>
    ///     Information about the Price
    /// </summary>
    public Price? price { get; set; }

    /// <summary>
    ///     timestamp in UTC unixseconds
    /// </summary>
    public string? date { get; set; }
}

/// <summary>
///     History Price Result
/// </summary>
public class HistoryPriceResult
{
    /// <summary>
    ///     Total number of history prices
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of history prices
    /// </summary>
    public HistoryPrice[]? history_prices { get; set; }
}

/// <summary>
///     Base Information of an AddressStorage
/// </summary>
public class AddressStorage
{
    /// <summary>
    ///     Avaliable
    /// </summary>
    public long? available { get; set; }

    /// <summary>
    ///     Used
    /// </summary>
    public long? used { get; set; }

    /// <summary>
    ///     Avatar
    /// </summary>
    public string? avatar { get; set; }
}

/// <summary>
///     Base Information of an AddressStakes
/// </summary>
public class AddressStakes
{
    /// <summary>
    ///     staked amount
    /// </summary>
    public string? amount { get; set; }

    /// <summary>
    ///     time in seconds
    /// </summary>
    public long? time { get; set; }

    /// <summary>
    ///     unclaimed amount
    /// </summary>
    public string? unclaimed { get; set; }
}

/// <summary>
///     Base Information of an AddressBalance
/// </summary>
public class AddressBalance
{
    /// <summary>
    ///     Token of the Balance
    /// </summary>
    public Token? token { get; set; }

    /// <summary>
    ///     Chain of the Balance
    /// </summary>
    public Chain? chain { get; set; }

    /// <summary>
    ///     Amount of the Balance
    /// </summary>
    public string? amount { get; set; }
}

/// <summary>
///     Base Information of an ValidatorKind
/// </summary>
public class ValidatorKind
{
    /// <summary>
    ///     Returns the kind name
    /// </summary>
    public string? name { get; set; }
}

/// <summary>
///     Validator Kind Result
/// </summary>
public class ValidatorKindResult
{
    /// <summary>
    ///     Total number of found validator kinds
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of available validator kinds
    /// </summary>
    public ValidatorKind[]? validator_kinds { get; set; }
}

/// <summary>
///     Base Information of a Contract Method History
/// </summary>
public class ContractMethodHistory
{
    /// <summary>
    ///     Contract
    /// </summary>
    public Contract? contract { get; set; }

    /// <summary>
    ///     unixsecond timestamp in UTC
    /// </summary>
    public string? date { get; set; }
}

/// <summary>
///     Contract Method History Result
/// </summary>
public class ContractMethodHistoryResult
{
    /// <summary>
    ///     Total number of found contracts
    /// </summary>
    public long? total_results { get; set; }

    /// <summary>
    ///     List of available contracts
    /// </summary>
    public ContractMethodHistory[]? Contract_method_histories { get; set; }
}

/// <summary>
///     Base Information of a TokenLogo
/// </summary>
public class TokenLogo
{
    /// <summary>
    ///     Logo Type
    /// </summary>
    public string? type { get; set; }

    /// <summary>
    ///     URL to the Logo
    /// </summary>
    public string? url { get; set; }
}

/// <summary>
///     Information what Endpoint might deliver a Result
/// </summary>
public class Search
{
    /// <summary>
    ///     Name of the Endpoint
    /// </summary>
    public string? endpoint_name { get; set; }

    /// <summary>
    ///     Name of the Parameter
    /// </summary>
    public string? endpoint_parameter { get; set; }

    /// <summary>
    ///     Was it found
    /// </summary>
    public bool found { get; set; }
}

/// <summary>
///     Search Result
/// </summary>
public class SearchResult
{
    /// <summary>
    ///     Search Result List
    /// </summary>
    public Search[]? result { get; set; }
}
