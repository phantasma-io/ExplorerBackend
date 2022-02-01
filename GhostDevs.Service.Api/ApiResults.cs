#nullable enable
namespace GhostDevs.Service.ApiResults;

public class JsonResult
{
    public string json { get; set; }
}

[APIDescription("Result of action, if success has a value of false it should be considered an error")]
public class BooleanResult
{
    public bool success { get; set; }
}

public class NftMetadata
{
    public string description { get; set; }
    public string name { get; set; }
    public string image { get; set; }
    public string video { get; set; }
    public string info_url { get; set; }
    public string rom { get; set; }
    public string ram { get; set; }
    public string mint_date { get; set; }
    public string mint_number { get; set; }
}

public class InfusedInto
{
    public string token_id { get; set; }
    public string chain { get; set; }
    public string contract { get; set; }
}

public class NftOwnershipResult
{
    public string address { get; set; }
    public string onchain_name { get; set; }
    public string offchain_name { get; set; }
    public int amount { get; set; }
}

public class Series
{
    public string id { get; set; }
    public string creator { get; set; }
    public int current_supply { get; set; }
    public int max_supply { get; set; }
    public string mode_name { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public string image { get; set; }
    public string royalties { get; set; }
    public int type { get; set; }
    public string attrType1 { get; set; }
    public string attrValue1 { get; set; }
    public string attrType2 { get; set; }
    public string attrValue2 { get; set; }
    public string attrType3 { get; set; }
    public string attrValue3 { get; set; }
}

public class Infusion
{
    public string key { get; set; }
    public string value { get; set; }
}

public class Nft
{
    public string token_id { get; set; }
    public string chain { get; set; }
    public string symbol { get; set; }
    public string creator_address { get; set; }
    public string creator_onchain_name { get; set; }
    public NftOwnershipResult[] owners { get; set; }
    public string contract { get; set; }
    public NftMetadata nft_metadata { get; set; }
    public Series series { get; set; }
    public Infusion[] infusion { get; set; }
    public InfusedInto? infused_into { get; set; }
}

public class NftsResult
{
    [APIDescription("Total number of found assets")]
    public long? total_results { get; set; }

    [APIDescription("List of available assets")]
    public Nft[] nfts { get; set; }
}

public class Event
{
    public string chain { get; set; }
    public string contract { get; set; }
    public string date { get; set; }
    public string transaction_hash { get; set; }
    public string token_id { get; set; }
    public int token_amount { get; set; }
    public string event_kind { get; set; }
    public string base_symbol { get; set; }
    public string quote_symbol { get; set; }
    public string price { get; set; }
    public string infused_symbol { get; set; }
    public string infused_value { get; set; }
    public string fiat_price { get; set; }
    public string fiat_currency { get; set; }
    public string source_address { get; set; }
    public string source_onchain_name { get; set; }
    public string address { get; set; }
    public string onchain_name { get; set; }
    public NftMetadata? nft_metadata { get; set; }
    public Series? series { get; set; }
}

public class EventsResult
{
    [APIDescription("Total number of found events")]
    public long? total_results { get; set; }

    [APIDescription("List of available events")]
    public Event[] events { get; set; }
}

public class SeriesResult
{
    [APIDescription("Total number of found series")]
    public long total_results { get; set; }

    [APIDescription("List of available series")]
    public Series[] series { get; set; }
}

public class Chain
{
    [APIDescription("Returns the chain name")]
    public string chain_name { get; set; }

    [APIDescription("Returns the chain height")]
    public string chain_height { get; set; }
}

public class ChainResult
{
    [APIDescription("total number of found chains")]
    public long total_results { get; set; }

    [APIDescription("List of available chains")]
    public Chain[] chains { get; set; }
}

public class EventKind
{
    [APIDescription("Returns the kind name")]
    public string kind { get; set; }

    //TODO maybe add chain here too
}

public class EventKindResult
{
    [APIDescription("Total number of found eventKinds")]
    public long? total_results { get; set; }

    [APIDescription("List of available eventKinds")]
    public EventKind[] eventKinds { get; set; }
}

public class Address
{
    [APIDescription("Returns the address")]
    public string address { get; set; }

    [APIDescription("Returns the address name")]
    public string address_name { get; set; }
}

public class AddressResult
{
    [APIDescription("Total number of found addresses")]
    public long? total_results { get; set; }

    [APIDescription("List of available addresses")]
    public Address[] addresses { get; set; }
}

//TODO add FIAT Prices from db
public class Token
{
    [APIDescription("Returns Symbol")] public string symbol { get; set; }

    [APIDescription("Returns fungible value of the token")]
    public bool fungible { get; set; }

    [APIDescription("Returns transferable value of the token")]
    public bool transferable { get; set; }

    [APIDescription("Returns finite value of the token")]
    public bool finite { get; set; }

    [APIDescription("Returns divisible value of the token")]
    public bool divisible { get; set; }

    [APIDescription("Returns fuel value of the token")]
    public bool fuel { get; set; }

    [APIDescription("Returns stakable value of the token")]
    public bool stakable { get; set; }

    [APIDescription("Returns fiat value of the token")]
    public bool fiat { get; set; }

    [APIDescription("Returns swappable value of the token")]
    public bool swappable { get; set; }

    [APIDescription("Returns burnable value of the token")]
    public bool burnable { get; set; }

    [APIDescription("Returns decimal value of the token")]
    public int decimals { get; set; }

    [APIDescription("Returns the current supply of the token")]
    public string current_supply { get; set; }

    [APIDescription("Returns the max supply of the token, 0 = infinite")]
    public string max_supply { get; set; }

    [APIDescription("Returns the burned supply of the token")]
    public string burned_supply { get; set; }

    [APIDescription("Returns the script of the token, raw")]
    public string script_raw { get; set; }
}

public class TokenResult
{
    [APIDescription("Total number of found tokens")]
    public long? total_results { get; set; }

    [APIDescription("List of available tokens")]
    public Token[] tokens { get; set; }
}

public class Transaction
{
    [APIDescription("Hash of the transaction")]
    public string hash { get; set; }

    [APIDescription("Height of the Block from the transaction")]
    public string blockHeight { get; set; }

    [APIDescription("index in the Block from the transaction")]
    public int index { get; set; }

    [APIDescription("List of Events from the transaction")]
    public Event[] events { get; set; }
}

public class TransactionResult
{
    [APIDescription("Total number of found transactions")]
    public long? total_results { get; set; }

    [APIDescription("List of available transactions")]
    public Transaction[] transactions { get; set; }
}

public class Contract
{
    [APIDescription("Name of the contract")]
    public string name { get; set; }

    [APIDescription("Hash of the contract")]
    public string hash { get; set; }

    [APIDescription("Symbol of the contract, if there is one")]
    public string symbol { get; set; }
}

public class ContractResult
{
    [APIDescription("Total number of found contracts")]
    public long? total_results { get; set; }

    [APIDescription("List of available contracts")]
    public Contract[] contracts { get; set; }
}

public class Instruction
{
    [APIDescription("Instruction")] public string instruction { get; set; }
}

public class DisassemblerResult
{
    [APIDescription("Total number of Instructions of the parsed Script")]
    public long? total_results { get; set; }

    [APIDescription("List instructions of the parsed Script")]
    public Instruction[] Instructions { get; set; }
}

public class Organization
{
    [APIDescription("Name of the organization")]
    public string name { get; set; }

    [APIDescription("List of Addresses from the organization")]
    public Address[] addresses { get; set; }
}

public class OrganizationResult
{
    [APIDescription("List of available organizations")]
    public Organization[] organizations { get; set; }
}
