using System;
using System.Globalization;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
using Address = GhostDevs.Service.ApiResults.Address;
using AddressEvent = GhostDevs.Service.ApiResults.AddressEvent;
using Block = GhostDevs.Service.ApiResults.Block;
using Chain = GhostDevs.Service.ApiResults.Chain;
using ChainEvent = GhostDevs.Service.ApiResults.ChainEvent;
using Event = GhostDevs.Service.ApiResults.Event;
using GasEvent = GhostDevs.Service.ApiResults.GasEvent;
using HashEvent = GhostDevs.Service.ApiResults.HashEvent;
using InfusionEvent = GhostDevs.Service.ApiResults.InfusionEvent;
using MarketEvent = GhostDevs.Service.ApiResults.MarketEvent;
using Oracle = GhostDevs.Service.ApiResults.Oracle;
using Organization = GhostDevs.Service.ApiResults.Organization;
using OrganizationEvent = GhostDevs.Service.ApiResults.OrganizationEvent;
using Platform = GhostDevs.Service.ApiResults.Platform;
using SaleEvent = GhostDevs.Service.ApiResults.SaleEvent;
using Series = GhostDevs.Service.ApiResults.Series;
using StringEvent = GhostDevs.Service.ApiResults.StringEvent;
using Token = GhostDevs.Service.ApiResults.Token;
using TokenEvent = GhostDevs.Service.ApiResults.TokenEvent;
using Transaction = GhostDevs.Service.ApiResults.Transaction;
using TransactionSettleEvent = GhostDevs.Service.ApiResults.TransactionSettleEvent;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(BlockResult), "Returns the block information from backend.", false, 10)]
    public BlockResult Blocks(
        [APIParameter("Order by [id, hash]", "string")]
        string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("hash", "string")] string hash = "",
        [APIParameter("hash (partial match)", "string")]
        string hash_partial = "",
        [APIParameter("height of the block", "string")]
        string height = "",
        [APIParameter("Return data with oracles of the block", "integer")]
        int with_oracles = 0,
        [APIParameter("Return data with transactions of the block", "integer")]
        int with_transactions = 0,
        [APIParameter("Return data with events of the transaction", "integer")]
        int with_events = 0,
        [APIParameter("Return event data of events", "integer")]
        int with_event_data = 0,
        [APIParameter("Return data with nft metadata", "integer")]
        int with_nft = 0
    )
    {
        Block[] blockArray;
        const string fiatCurrency = "USD";

        using ( var databaseContext = new MainDbContext() )
        {
            try
            {
                if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                    throw new APIException("Unsupported value for 'order_by' parameter.");

                if ( !ArgValidation.CheckOrderDirection(order_direction) )
                    throw new APIException("Unsupported value for 'order_direction' parameter.");

                if ( !ArgValidation.CheckLimit(limit) )
                    throw new APIException("Unsupported value for 'limit' parameter.");

                if ( !string.IsNullOrEmpty(hash) && !ArgValidation.CheckString(hash) )
                    throw new APIException("Unsupported value for 'hash' parameter.");

                if ( !string.IsNullOrEmpty(hash_partial) && !ArgValidation.CheckAddress(hash_partial) )
                    throw new APIException("Unsupported value for 'hash_partial' parameter.");

                if ( !string.IsNullOrEmpty(height) && !ArgValidation.CheckNumber(height) )
                    throw new APIException("Unsupported value for 'height' parameter.");

                var startTime = DateTime.Now;
                var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);


                //just need that since we build the model so it knows what we can use
                var query = databaseContext.Blocks.AsQueryable();

                if ( !string.IsNullOrEmpty(hash) )
                    query = query.Where(x => string.Equals(x.HASH.ToUpper(), hash.ToUpper()));

                if ( !string.IsNullOrEmpty(hash_partial) )
                    query = query.Where(x => x.HASH.ToUpper().Contains(hash_partial.ToUpper()));

                if ( !string.IsNullOrEmpty(height) )
                    query = query.Where(x => string.Equals(x.HEIGHT, height));

                //in case we add more to sort
                if ( order_direction == "asc" )
                    query = order_by switch
                    {
                        "id" => query.OrderBy(x => x.ID),
                        "hash" => query.OrderBy(x => x.HASH),
                        _ => query
                    };
                else
                    query = order_by switch
                    {
                        "id" => query.OrderByDescending(x => x.ID),
                        "hash" => query.OrderByDescending(x => x.HASH),
                        _ => query
                    };

                var queryResults = query.Skip(offset).Take(limit).ToList();


                blockArray = queryResults.Select(x => new Block
                {
                    height = x.HEIGHT,
                    hash = x.HASH,
                    previous_hash = x.PREVIOUS_HASH,
                    protocol = x.PROTOCOL,
                    chain_address = x.ChainAddress.ADDRESS,
                    validator_address = x.ValidatorAddress.ADDRESS,
                    oracles = ( with_oracles == 1 && x.BlockOracles != null
                        ? x.BlockOracles.Select(o => new Oracle
                        {
                            url = o.Oracle.URL,
                            content = o.Oracle.CONTENT
                        }).ToArray()
                        : null ) ?? Array.Empty<Oracle>(),
                    Transactions = ( with_transactions == 1 && x.Transactions != null
                        ? x.Transactions.Select(t => new Transaction
                        {
                            hash = t.HASH,
                            blockHeight = x.HEIGHT,
                            index = t.INDEX,
                            events = ( with_events == 1 && t.Events != null
                                ? t.Events.Select(e => new Event
                                {
                                    chain = e.Chain.NAME.ToLower(),
                                    contract = ContractMethods.Prepend0x(e.Contract.HASH, e.Chain.NAME),
                                    date = e.TIMESTAMP_UNIX_SECONDS.ToString(),
                                    transaction_hash = x.HASH, //a bit redundant in that case
                                    token_id = e.TOKEN_ID,
                                    token_amount = e.TOKEN_AMOUNT,
                                    event_kind = e.EventKind.NAME,
                                    base_symbol = e.Contract.SYMBOL,
                                    price = e.PRICE,
                                    fiat_price = FiatExchangeRateMethods
                                        .Convert(fiatPricesInUsd, e.PRICE_USD, "USD", fiatCurrency)
                                        .ToString("0.####"),
                                    fiat_currency = fiatCurrency,
                                    quote_symbol = e.QuoteSymbol?.SYMBOL!,
                                    infused_symbol = e.InfusedSymbol?.SYMBOL!,
                                    infused_value = e.INFUSED_VALUE,
                                    address = AddressMethods.Prepend0x(e.Address.ADDRESS, e.Chain.NAME),
                                    onchain_name = e.Address.ADDRESS_NAME,
                                    source_address = AddressMethods.Prepend0x(e.SourceAddress?.ADDRESS, e.Chain.NAME),
                                    source_onchain_name = e.SourceAddress?.ADDRESS_NAME!,
                                    nft_metadata = with_nft == 1 && e.Nft != null
                                        ? new NftMetadata
                                        {
                                            name = e.Nft.NAME,
                                            description = e.Nft.DESCRIPTION,
                                            image = e.Nft.IMAGE,
                                            video = e.Nft.VIDEO,
                                            rom = e.Nft.ROM,
                                            ram = e.Nft.RAM,
                                            mint_date = e.Nft.MINT_DATE_UNIX_SECONDS.ToString(),
                                            mint_number = e.Nft.MINT_NUMBER.ToString()
                                        }
                                        : null,
                                    series = with_nft == 1 && e.Nft?.Series != null
                                        ? new Series
                                        {
                                            id = e.Nft.Series.SERIES_ID,
                                            creator =
                                                AddressMethods.Prepend0x(e.Nft.Series.CreatorAddress?.ADDRESS,
                                                    e.Chain.NAME),
                                            current_supply = e.Nft.Series.CURRENT_SUPPLY,
                                            max_supply = e.Nft.Series.MAX_SUPPLY,
                                            mode_name = e.Nft.Series.SeriesMode?.MODE_NAME!,
                                            name = e.Nft.Series.NAME,
                                            description = e.Nft.Series.DESCRIPTION,
                                            image = e.Nft.Series.IMAGE,
                                            royalties = e.Nft.Series.ROYALTIES.ToString(CultureInfo.InvariantCulture),
                                            type = e.Nft.Series.TYPE,
                                            attrType1 = e.Nft.Series.ATTR_TYPE_1,
                                            attrValue1 = e.Nft.Series.ATTR_VALUE_1,
                                            attrType2 = e.Nft.Series.ATTR_TYPE_2,
                                            attrValue2 = e.Nft.Series.ATTR_VALUE_2,
                                            attrType3 = e.Nft.Series.ATTR_TYPE_3,
                                            attrValue3 = e.Nft.Series.ATTR_VALUE_3
                                        }
                                        : null,
                                    address_event = with_event_data == 1 && e.AddressEvent != null
                                        ? new AddressEvent
                                        {
                                            address = e.AddressEvent.Address != null
                                                ? new Address
                                                {
                                                    address_name = e.AddressEvent.Address.ADDRESS_NAME,
                                                    address = e.AddressEvent.Address.ADDRESS
                                                }
                                                : null
                                        }
                                        : null,
                                    chain_event = with_event_data == 1 && e.ChainEvent != null
                                        ? new ChainEvent
                                        {
                                            name = e.ChainEvent.NAME,
                                            value = e.ChainEvent.VALUE,
                                            chain = e.ChainEvent.Chain != null
                                                ? new Chain
                                                {
                                                    chain_name = e.ChainEvent.Chain.NAME
                                                }
                                                : null
                                        }
                                        : null,
                                    gas_event = with_event_data == 1 && e.GasEvent != null
                                        ? new GasEvent
                                        {
                                            price = e.GasEvent.PRICE,
                                            amount = e.GasEvent.AMOUNT,
                                            address = e.GasEvent.Address != null
                                                ? new Address
                                                {
                                                    address = e.GasEvent.Address.ADDRESS,
                                                    address_name = e.GasEvent.Address.ADDRESS_NAME
                                                }
                                                : null
                                        }
                                        : null,
                                    hash_event = with_event_data == 1 && e.HashEvent != null
                                        ? new HashEvent
                                        {
                                            hash = e.HashEvent.HASH
                                        }
                                        : null,
                                    infusion_event = with_event_data == 1 && e.InfusionEvent != null
                                        ? new InfusionEvent
                                        {
                                            token_id = e.InfusionEvent.TOKEN_ID,
                                            infused_value = e.InfusionEvent.INFUSED_VALUE,
                                            base_token = e.InfusionEvent.BaseToken != null
                                                ? new Token
                                                {
                                                    symbol = e.InfusionEvent.BaseToken.SYMBOL,
                                                    fungible = e.InfusionEvent.BaseToken.FUNGIBLE,
                                                    transferable = e.InfusionEvent.BaseToken.TRANSFERABLE,
                                                    finite = e.InfusionEvent.BaseToken.FINITE,
                                                    divisible = e.InfusionEvent.BaseToken.DIVISIBLE,
                                                    fiat = e.InfusionEvent.BaseToken.FIAT,
                                                    fuel = e.InfusionEvent.BaseToken.FUEL,
                                                    swappable = e.InfusionEvent.BaseToken.SWAPPABLE,
                                                    burnable = e.InfusionEvent.BaseToken.BURNABLE,
                                                    stakable = e.InfusionEvent.BaseToken.STAKABLE,
                                                    decimals = e.InfusionEvent.BaseToken.DECIMALS
                                                    /*current_supply = e.InfusionEvent.BaseToken.CURRENT_SUPPLY,
                                                    max_supply = e.InfusionEvent.BaseToken.MAX_SUPPLY,
                                                    burned_supply = e.InfusionEvent.BaseToken.BURNED_SUPPLY,
                                                    script_raw = e.InfusionEvent.BaseToken.SCRIPT_RAW*/
                                                }
                                                : null,
                                            infused_token = e.InfusionEvent.InfusedToken != null
                                                ? new Token
                                                {
                                                    symbol = e.InfusionEvent.InfusedToken.SYMBOL,
                                                    fungible = e.InfusionEvent.InfusedToken.FUNGIBLE,
                                                    transferable = e.InfusionEvent.InfusedToken.TRANSFERABLE,
                                                    finite = e.InfusionEvent.InfusedToken.FINITE,
                                                    divisible = e.InfusionEvent.InfusedToken.DIVISIBLE,
                                                    fiat = e.InfusionEvent.InfusedToken.FIAT,
                                                    fuel = e.InfusionEvent.InfusedToken.FUEL,
                                                    swappable = e.InfusionEvent.InfusedToken.SWAPPABLE,
                                                    burnable = e.InfusionEvent.InfusedToken.BURNABLE,
                                                    stakable = e.InfusionEvent.InfusedToken.STAKABLE,
                                                    decimals = e.InfusionEvent.InfusedToken.DECIMALS
                                                    /*current_supply = e.InfusionEvent.InfusedToken.CURRENT_SUPPLY,
                                                    max_supply = e.InfusionEvent.InfusedToken.MAX_SUPPLY,
                                                    burned_supply = e.InfusionEvent.InfusedToken.BURNED_SUPPLY,
                                                    script_raw = e.InfusionEvent.InfusedToken.SCRIPT_RAW*/
                                                }
                                                : null
                                        }
                                        : null,
                                    market_event = with_event_data == 1 && e.MarketEvent != null
                                        ? new MarketEvent
                                        {
                                            base_token = e.MarketEvent.BaseToken != null
                                                ? new Token
                                                {
                                                    symbol = e.MarketEvent.BaseToken.SYMBOL,
                                                    fungible = e.MarketEvent.BaseToken.FUNGIBLE,
                                                    transferable = e.MarketEvent.BaseToken.TRANSFERABLE,
                                                    finite = e.MarketEvent.BaseToken.FINITE,
                                                    divisible = e.MarketEvent.BaseToken.DIVISIBLE,
                                                    fiat = e.MarketEvent.BaseToken.FIAT,
                                                    fuel = e.MarketEvent.BaseToken.FUEL,
                                                    swappable = e.MarketEvent.BaseToken.SWAPPABLE,
                                                    burnable = e.MarketEvent.BaseToken.BURNABLE,
                                                    stakable = e.MarketEvent.BaseToken.STAKABLE,
                                                    decimals = e.MarketEvent.BaseToken.DECIMALS
                                                    /*current_supply = e.MarketEvent.BaseToken.CURRENT_SUPPLY,
                                                    max_supply = e.MarketEvent.BaseToken.MAX_SUPPLY,
                                                    burned_supply = e.MarketEvent.BaseToken.BURNED_SUPPLY,
                                                    script_raw = e.MarketEvent.BaseToken.SCRIPT_RAW*/
                                                }
                                                : null,
                                            quote_token = e.MarketEvent.QuoteToken != null
                                                ? new Token
                                                {
                                                    symbol = e.MarketEvent.QuoteToken.SYMBOL,
                                                    fungible = e.MarketEvent.QuoteToken.FUNGIBLE,
                                                    transferable = e.MarketEvent.QuoteToken.TRANSFERABLE,
                                                    finite = e.MarketEvent.QuoteToken.FINITE,
                                                    divisible = e.MarketEvent.QuoteToken.DIVISIBLE,
                                                    fiat = e.MarketEvent.QuoteToken.FIAT,
                                                    fuel = e.MarketEvent.QuoteToken.FUEL,
                                                    swappable = e.MarketEvent.QuoteToken.SWAPPABLE,
                                                    burnable = e.MarketEvent.QuoteToken.BURNABLE,
                                                    stakable = e.MarketEvent.QuoteToken.STAKABLE,
                                                    decimals = e.MarketEvent.QuoteToken.DECIMALS
                                                    /*current_supply = e.MarketEvent.QuoteToken.CURRENT_SUPPLY,
                                                    max_supply = e.MarketEvent.QuoteToken.MAX_SUPPLY,
                                                    burned_supply = e.MarketEvent.QuoteToken.BURNED_SUPPLY,
                                                    script_raw = e.MarketEvent.QuoteToken.SCRIPT_RAW*/
                                                }
                                                : null,
                                            end_price = e.MarketEvent.END_PRICE,
                                            price = e.MarketEvent.PRICE,
                                            market_event_kind = e.MarketEvent.MarketEventKind.NAME,
                                            market_id = e.MarketEvent.MARKET_ID
                                        }
                                        : null,
                                    organization_event = with_event_data == 1 && e.OrganizationEvent != null
                                        ? new OrganizationEvent
                                        {
                                            organization = e.OrganizationEvent.Organization != null
                                                ? new Organization
                                                {
                                                    name = e.OrganizationEvent.Organization.NAME,
                                                    addresses = null //atm we do not include them here
                                                }
                                                : null,
                                            address = e.OrganizationEvent.Address != null
                                                ? new Address
                                                {
                                                    address = e.OrganizationEvent.Address.ADDRESS,
                                                    address_name = e.OrganizationEvent.Address.ADDRESS_NAME
                                                }
                                                : null
                                        }
                                        : null,
                                    sale_event = with_event_data == 1 && e.SaleEvent != null
                                        ? new SaleEvent
                                        {
                                            hash = e.SaleEvent.HASH,
                                            sale_event_kind = e.SaleEvent.SaleEventKind.NAME
                                        }
                                        : null,
                                    string_event = with_event_data == 1 && e.StringEvent != null
                                        ? new StringEvent
                                        {
                                            string_value = e.StringEvent.STRING_VALUE
                                        }
                                        : null,
                                    token_event = with_event_data == 1 && e.TokenEvent != null
                                        ? new TokenEvent
                                        {
                                            token = e.TokenEvent.Token != null
                                                ? new Token
                                                {
                                                    symbol = e.TokenEvent.Token.SYMBOL,
                                                    fungible = e.TokenEvent.Token.FUNGIBLE,
                                                    transferable = e.TokenEvent.Token.TRANSFERABLE,
                                                    finite = e.TokenEvent.Token.FINITE,
                                                    divisible = e.TokenEvent.Token.DIVISIBLE,
                                                    fiat = e.TokenEvent.Token.FIAT,
                                                    fuel = e.TokenEvent.Token.FUEL,
                                                    swappable = e.TokenEvent.Token.SWAPPABLE,
                                                    burnable = e.TokenEvent.Token.BURNABLE,
                                                    stakable = e.TokenEvent.Token.STAKABLE,
                                                    decimals = e.TokenEvent.Token.DECIMALS
                                                    /*current_supply = e.TokenEvent.Token.CURRENT_SUPPLY,
                                                    max_supply = e.TokenEvent.Token.MAX_SUPPLY,
                                                    burned_supply = e.TokenEvent.Token.BURNED_SUPPLY,
                                                    script_raw = e.TokenEvent.Token.SCRIPT_RAW*/
                                                }
                                                : null,
                                            value = e.TokenEvent.VALUE,
                                            chain_name = e.TokenEvent.CHAIN_NAME
                                        }
                                        : null,
                                    transaction_settle_event = with_event_data == 1 && e.TransactionSettleEvent != null
                                        ? new TransactionSettleEvent
                                        {
                                            hash = e.TransactionSettleEvent.HASH,
                                            platform = e.TransactionSettleEvent.Platform != null
                                                ? new Platform
                                                {
                                                    name = e.TransactionSettleEvent.Platform.NAME,
                                                    chain = e.TransactionSettleEvent.Platform.CHAIN,
                                                    fuel = e.TransactionSettleEvent.Platform.FUEL
                                                    //we do not add other information here for now
                                                }
                                                : null
                                        }
                                        : null
                                }).ToArray()
                                : null ) ?? Array.Empty<Event>()
                        }).ToArray()
                        : null ) ?? Array.Empty<Transaction>()
                }).ToArray();


                var responseTime = DateTime.Now - startTime;
                Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
            }
            catch ( APIException )
            {
                throw;
            }
            catch ( Exception exception )
            {
                var logMessage = LogEx.Exception("Block()", exception);

                throw new APIException(logMessage, exception);
            }
        }

        return new BlockResult {blocks = blockArray};
    }
}
