using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Address = GhostDevs.Service.ApiResults.Address;
using AddressEvent = GhostDevs.Service.ApiResults.AddressEvent;
using Chain = GhostDevs.Service.ApiResults.Chain;
using ChainEvent = GhostDevs.Service.ApiResults.ChainEvent;
using Contract = GhostDevs.Service.ApiResults.Contract;
using Event = GhostDevs.Service.ApiResults.Event;
using GasEvent = GhostDevs.Service.ApiResults.GasEvent;
using HashEvent = GhostDevs.Service.ApiResults.HashEvent;
using InfusionEvent = GhostDevs.Service.ApiResults.InfusionEvent;
using MarketEvent = GhostDevs.Service.ApiResults.MarketEvent;
using Organization = GhostDevs.Service.ApiResults.Organization;
using OrganizationEvent = GhostDevs.Service.ApiResults.OrganizationEvent;
using Platform = GhostDevs.Service.ApiResults.Platform;
using SaleEvent = GhostDevs.Service.ApiResults.SaleEvent;
using Series = GhostDevs.Service.ApiResults.Series;
using StringEvent = GhostDevs.Service.ApiResults.StringEvent;
using Token = GhostDevs.Service.ApiResults.Token;
using TokenEvent = GhostDevs.Service.ApiResults.TokenEvent;
using TransactionSettleEvent = GhostDevs.Service.ApiResults.TransactionSettleEvent;

namespace GhostDevs.Service;

public partial class Endpoints
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns events available on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-EventsResult'>EventsResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are date, token_id or id</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="contract" example="SOUL">Token contract hash</param>
    /// <param name="token_id">Token ID</param>
    /// <param name="date_day">Date day match (matches whole given day)</param>
    /// <param name="date_less">Date (greater than), UTC unixseconds</param>
    /// <param name="date_greater">Date (greater than), UTC unixseconds</param>
    /// <param name="event_kind" example="TokenMint">Event kind name</param>
    /// <param name="event_kind_partial" example="Token">Event kind (partial match)</param>
    /// <param name="nft_name_partial">Nft name (partial match)</param>
    /// <param name="nft_description_partial">Nft description (partial match))</param>
    /// <param name="address">Address (Hash)</param>
    /// <param name="address_partial">Address (partial match) (Hash)</param>
    /// <param name="block_hash"><a href='#model-Block'>Block</a> hash</param>
    /// <param name="block_height">height of the <a href='#model-Block'>Block</a></param>
    /// <param name="transaction_hash"><a href='#model-Transaction'>Transaction</a> hash</param>
    /// <param name="event_id">Internal ID</param>
    /// <param name="with_event_data" example="0">Return event data with more details, needs with_events to be set</param>
    /// <param name="with_metadata" example="0">Return data with <a href='#model-NftMetadata'>nft metadata</a></param>
    /// <param name="with_series" example="0">Return NFT <a href='#model-Series'>Series</a></param>
    /// <param name="with_fiat" example="0">
    ///     Return with <a href='#model-FiatPrice'>fiat_prices</a> (only
    ///     <a href='#model-MarketEvent'>market_event</a>)
    /// </param>
    /// <param name="with_nsfw" example="0">Include Data that has been marked NSFW</param>
    /// <param name="with_blacklisted" example="0">Include Data that has been marked Blacklisted</param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Ok</response>
    [ProducesResponseType(typeof(EventsResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [APIInfo(typeof(EventsResult), "Returns events available on the backend.", false, 10, cacheTag: "events")]
    public EventsResult Events(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string chain = "",
        string contract = "",
        string token_id = "",
        string date_day = "",
        string date_less = "",
        string date_greater = "",
        string event_kind = "",
        string event_kind_partial = "",
        string nft_name_partial = "",
        string nft_description_partial = "",
        string address = "",
        string address_partial = "",
        string block_hash = "",
        string block_height = "",
        string transaction_hash = "",
        string event_id = "",
        int with_event_data = 0,
        int with_metadata = 0,
        int with_series = 0,
        int with_fiat = 0,
        int with_nsfw = 0,
        int with_blacklisted = 0,
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        // Results of the query
        long totalResults = 0;
        Event[] eventsArray;
        const string fiatCurrency = "USD";

        //chain is not considered a filter atm
        var filter = !string.IsNullOrEmpty(contract) || !string.IsNullOrEmpty(token_id) ||
                     !string.IsNullOrEmpty(date_day) || !string.IsNullOrEmpty(date_less) ||
                     !string.IsNullOrEmpty(date_greater) || !string.IsNullOrEmpty(event_kind) ||
                     !string.IsNullOrEmpty(event_kind_partial) || !string.IsNullOrEmpty(nft_name_partial) ||
                     !string.IsNullOrEmpty(nft_description_partial) || !string.IsNullOrEmpty(address_partial) ||
                     !string.IsNullOrEmpty(block_hash) || !string.IsNullOrEmpty(block_height) ||
                     !string.IsNullOrEmpty(transaction_hash);

        try
        {
            #region ArgValidation

            if ( !ArgValidation.CheckLimit(limit, filter) )
                throw new APIException("Unsupported value for 'limit' parameter.");

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new APIException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new APIException("Unsupported value for 'order_direction' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new APIException("Unsupported value for 'chain' parameter.");

            if ( !string.IsNullOrEmpty(contract) && !ArgValidation.CheckHash(contract, true) )
                throw new APIException("Unsupported value for 'contract' parameter.");

            ContractMethods.Drop0x(ref contract);

            if ( !string.IsNullOrEmpty(token_id) && !ArgValidation.CheckTokenId(token_id) )
                throw new APIException("Unsupported value for 'token_id' parameter.");

            if ( !string.IsNullOrEmpty(date_day) && !Regex.IsMatch(date_day, @"^[0-9.]+$") )
                throw new APIException("Unsupported value for 'date_day' parameter.");

            if ( !string.IsNullOrEmpty(date_less) && !ArgValidation.CheckNumber(date_less) )
                throw new APIException("Unsupported value for 'date_less' parameter.");

            if ( !string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckNumber(date_greater) )
                throw new APIException("Unsupported value for 'date_greater' parameter.");

            if ( !string.IsNullOrEmpty(event_kind) && !ArgValidation.CheckString(event_kind, true) )
                throw new APIException("Unsupported value for 'event_kind' parameter.");

            if ( !string.IsNullOrEmpty(event_kind_partial) && !ArgValidation.CheckString(event_kind_partial, true) )
                throw new APIException("Unsupported value for 'event_kind_partial' parameter.");

            if ( !string.IsNullOrEmpty(nft_name_partial) && !ArgValidation.CheckName(nft_name_partial) )
                throw new APIException("Unsupported value for 'nft_name_partial' parameter.");

            if ( !string.IsNullOrEmpty(nft_description_partial) &&
                 !Regex.IsMatch(nft_description_partial, @"^[_\-a-zA-Z0-9]+$") )
                throw new APIException("Unsupported value for 'nft_description_partial' parameter.");

            if ( !string.IsNullOrEmpty(address) && !ArgValidation.CheckAddress(address) )
                throw new APIException("Unsupported value for 'address' parameter.");

            ContractMethods.Drop0x(ref address);

            if ( !string.IsNullOrEmpty(address) && string.IsNullOrEmpty(chain) )
                throw new APIException("Pass chain when using address filter.");

            if ( !string.IsNullOrEmpty(address_partial) && !ArgValidation.CheckAddress(address_partial) )
                throw new APIException("Unsupported value for 'address_partial' parameter.");

            ContractMethods.Drop0x(ref address_partial);

            if ( !string.IsNullOrEmpty(block_hash) && !ArgValidation.CheckHash(block_hash) )
                throw new APIException("Unsupported value for 'block_hash' parameter.");

            if ( !string.IsNullOrEmpty(block_height) && !ArgValidation.CheckNumber(block_height) )
                throw new APIException("Unsupported value for 'block_height' parameter.");

            if ( !string.IsNullOrEmpty(transaction_hash) && !ArgValidation.CheckHash(transaction_hash) )
                throw new APIException("Unsupported value for 'transaction_hash' parameter.");

            if ( !string.IsNullOrEmpty(event_id) && !ArgValidation.CheckNumber(event_id) )
                throw new APIException("Unsupported value for 'event_id' parameter.");

            #endregion

            var startTime = DateTime.Now;
            using MainDbContext databaseContext = new();
            var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);


            // Getting exchange rates in advance.
            var query = databaseContext.Events.AsQueryable().AsNoTracking();

            #region Filtering

            if ( with_nsfw == 0 )
                query = query.Where(x => x.NSFW != true);

            if ( with_blacklisted == 0 )
                query = query.Where(x => x.BLACKLISTED != true);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Chain.NAME == chain);

            if ( !string.IsNullOrEmpty(token_id) ) query = query.Where(x => x.TOKEN_ID == token_id);

            if ( !string.IsNullOrEmpty(contract) ) query = query.Where(x => x.Contract.HASH == contract);

            if ( !string.IsNullOrEmpty(date_day) )
                query = query.Where(x => x.DATE_UNIX_SECONDS == UnixSeconds.FromDateTimeString(date_day));

            if ( !string.IsNullOrEmpty(date_less) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

            if ( !string.IsNullOrEmpty(date_greater) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

            if ( !string.IsNullOrEmpty(event_kind) ) query = query.Where(x => x.EventKind.NAME == event_kind);

            if ( !string.IsNullOrEmpty(event_kind_partial) )
                query = query.Where(x => x.EventKind.NAME.Contains(event_kind_partial));

            if ( !string.IsNullOrEmpty(nft_name_partial) )
                query = query.Where(x => x.Nft.NAME.Contains(nft_name_partial));

            if ( !string.IsNullOrEmpty(nft_description_partial) )
                query = query.Where(x => x.Nft.DESCRIPTION.Contains(nft_description_partial));

            if ( !string.IsNullOrEmpty(address) ) query = query.Where(x => x.Address.ADDRESS == address);

            if ( !string.IsNullOrEmpty(address_partial) )
                query = query.Where(x => x.Address.ADDRESS.Contains(address_partial) ||
                                         x.Address.ADDRESS_NAME.Contains(address_partial) ||
                                         x.Address.USER_NAME.Contains(address_partial));

            if ( !string.IsNullOrEmpty(block_hash) )
                query = query.Where(x => x.Transaction.Block.HASH == block_hash);

            if ( !string.IsNullOrEmpty(block_height) )
                query = query.Where(x => x.Transaction.Block.HEIGHT == block_height);

            if ( !string.IsNullOrEmpty(transaction_hash) )
                query = query.Where(x => x.Transaction.HASH == transaction_hash);

            if ( !string.IsNullOrEmpty(event_id) && int.TryParse(event_id, out var parsedEventId) )
                query = query.Where(x => x.ID == parsedEventId);

            #endregion

            if ( with_total == 1 )
                // Count total number of results before adding order and limit parts of query.
                totalResults = query.Count();

            if ( order_direction == "asc" )
                query = order_by switch
                {
                    "date" => query.OrderBy(x => x.TIMESTAMP_UNIX_SECONDS),
                    "token_id" => query.OrderBy(x => x.TOKEN_ID),
                    "id" => query.OrderBy(x => x.ID),
                    _ => query
                };
            else
                query = order_by switch
                {
                    "date" => query.OrderByDescending(x => x.TIMESTAMP_UNIX_SECONDS),
                    "token_id" => query.OrderByDescending(x => x.TOKEN_ID),
                    "id" => query.OrderByDescending(x => x.ID),
                    _ => query
                };

            #region ResultArray

            if ( limit > 0 ) query = query.Skip(offset).Take(limit);

            eventsArray = query.Select(x => new Event
                {
                    event_id = x.ID,
                    chain = x.Chain.NAME.ToLower(),
                    date = x.TIMESTAMP_UNIX_SECONDS.ToString(),
                    block_hash = x.Transaction.Block.HASH,
                    transaction_hash = x.Transaction.HASH,
                    token_id = x.TOKEN_ID,
                    event_kind = x.EventKind.NAME,
                    address = x.Address.ADDRESS,
                    address_name = x.Address.ADDRESS_NAME,
                    contract = new Contract
                    {
                        name = x.Contract.NAME,
                        hash = ContractMethods.Prepend0x(x.Contract.HASH, x.Chain.NAME),
                        symbol = x.Contract.SYMBOL
                    },
                    nft_metadata = with_metadata == 1 && x.Nft != null
                        ? new NftMetadata
                        {
                            name = x.Nft.NAME,
                            description = x.Nft.DESCRIPTION,
                            image = x.Nft.IMAGE,
                            video = x.Nft.VIDEO,
                            rom = x.Nft.ROM,
                            ram = x.Nft.RAM,
                            mint_date = x.Nft.MINT_DATE_UNIX_SECONDS.ToString(),
                            mint_number = x.Nft.MINT_NUMBER.ToString()
                        }
                        : null,
                    series = with_series == 1 && x.Nft != null && x.Nft.Series != null
                        ? new Series
                        {
                            id = x.Nft.Series.ID,
                            series_id = x.Nft.Series.SERIES_ID,
                            creator = x.Nft.Series.CreatorAddress != null
                                ? x.Nft.Series.CreatorAddress.ADDRESS
                                : null,
                            current_supply = x.Nft.Series.CURRENT_SUPPLY,
                            max_supply = x.Nft.Series.MAX_SUPPLY,
                            mode_name = x.Nft.Series.SeriesMode != null ? x.Nft.Series.SeriesMode.MODE_NAME : null,
                            name = x.Nft.Series.NAME,
                            description = x.Nft.Series.DESCRIPTION,
                            image = x.Nft.Series.IMAGE,
                            royalties = x.Nft.Series.ROYALTIES.ToString(CultureInfo.InvariantCulture),
                            type = x.Nft.Series.TYPE,
                            attr_type_1 = x.Nft.Series.ATTR_TYPE_1,
                            attr_value_1 = x.Nft.Series.ATTR_VALUE_1,
                            attr_type_2 = x.Nft.Series.ATTR_TYPE_2,
                            attr_value_2 = x.Nft.Series.ATTR_VALUE_2,
                            attr_type_3 = x.Nft.Series.ATTR_TYPE_3,
                            attr_value_3 = x.Nft.Series.ATTR_VALUE_3
                        }
                        : null,
                    address_event = with_event_data == 1 && x.AddressEvent != null
                        ? new AddressEvent
                        {
                            address = x.AddressEvent.Address != null
                                ? new Address
                                {
                                    address_name = x.AddressEvent.Address.ADDRESS_NAME,
                                    address = x.AddressEvent.Address.ADDRESS
                                }
                                : null
                        }
                        : null,
                    chain_event = with_event_data == 1 && x.ChainEvent != null
                        ? new ChainEvent
                        {
                            name = x.ChainEvent.NAME,
                            value = x.ChainEvent.VALUE,
                            chain = x.ChainEvent.Chain != null
                                ? new Chain
                                {
                                    chain_name = x.ChainEvent.Chain.NAME
                                }
                                : null
                        }
                        : null,
                    gas_event = with_event_data == 1 && x.GasEvent != null
                        ? new GasEvent
                        {
                            price = x.GasEvent.PRICE,
                            amount = x.GasEvent.AMOUNT,
                            address = x.GasEvent.Address != null
                                ? new Address
                                {
                                    address = x.GasEvent.Address.ADDRESS,
                                    address_name = x.GasEvent.Address.ADDRESS_NAME
                                }
                                : null
                        }
                        : null,
                    hash_event = with_event_data == 1 && x.HashEvent != null
                        ? new HashEvent
                        {
                            hash = x.HashEvent.HASH
                        }
                        : null,
                    infusion_event = with_event_data == 1 && x.InfusionEvent != null
                        ? new InfusionEvent
                        {
                            token_id = x.InfusionEvent.TOKEN_ID,
                            infused_value = x.InfusionEvent.INFUSED_VALUE,
                            base_token = x.InfusionEvent.BaseToken != null
                                ? new Token
                                {
                                    symbol = x.InfusionEvent.BaseToken.SYMBOL,
                                    fungible = x.InfusionEvent.BaseToken.FUNGIBLE,
                                    transferable = x.InfusionEvent.BaseToken.TRANSFERABLE,
                                    finite = x.InfusionEvent.BaseToken.FINITE,
                                    divisible = x.InfusionEvent.BaseToken.DIVISIBLE,
                                    fiat = x.InfusionEvent.BaseToken.FIAT,
                                    fuel = x.InfusionEvent.BaseToken.FUEL,
                                    swappable = x.InfusionEvent.BaseToken.SWAPPABLE,
                                    burnable = x.InfusionEvent.BaseToken.BURNABLE,
                                    stakable = x.InfusionEvent.BaseToken.STAKABLE,
                                    decimals = x.InfusionEvent.BaseToken.DECIMALS
                                }
                                : null,
                            infused_token = x.InfusionEvent.InfusedToken != null
                                ? new Token
                                {
                                    symbol = x.InfusionEvent.InfusedToken.SYMBOL,
                                    fungible = x.InfusionEvent.InfusedToken.FUNGIBLE,
                                    transferable = x.InfusionEvent.InfusedToken.TRANSFERABLE,
                                    finite = x.InfusionEvent.InfusedToken.FINITE,
                                    divisible = x.InfusionEvent.InfusedToken.DIVISIBLE,
                                    fiat = x.InfusionEvent.InfusedToken.FIAT,
                                    fuel = x.InfusionEvent.InfusedToken.FUEL,
                                    swappable = x.InfusionEvent.InfusedToken.SWAPPABLE,
                                    burnable = x.InfusionEvent.InfusedToken.BURNABLE,
                                    stakable = x.InfusionEvent.InfusedToken.STAKABLE,
                                    decimals = x.InfusionEvent.InfusedToken.DECIMALS
                                }
                                : null
                        }
                        : null,
                    market_event = with_event_data == 1 && x.MarketEvent != null
                        ? new MarketEvent
                        {
                            base_token = x.MarketEvent.BaseToken != null
                                ? new Token
                                {
                                    symbol = x.MarketEvent.BaseToken.SYMBOL,
                                    fungible = x.MarketEvent.BaseToken.FUNGIBLE,
                                    transferable = x.MarketEvent.BaseToken.TRANSFERABLE,
                                    finite = x.MarketEvent.BaseToken.FINITE,
                                    divisible = x.MarketEvent.BaseToken.DIVISIBLE,
                                    fiat = x.MarketEvent.BaseToken.FIAT,
                                    fuel = x.MarketEvent.BaseToken.FUEL,
                                    swappable = x.MarketEvent.BaseToken.SWAPPABLE,
                                    burnable = x.MarketEvent.BaseToken.BURNABLE,
                                    stakable = x.MarketEvent.BaseToken.STAKABLE,
                                    decimals = x.MarketEvent.BaseToken.DECIMALS
                                }
                                : null,
                            quote_token = x.MarketEvent.QuoteToken != null
                                ? new Token
                                {
                                    symbol = x.MarketEvent.QuoteToken.SYMBOL,
                                    fungible = x.MarketEvent.QuoteToken.FUNGIBLE,
                                    transferable = x.MarketEvent.QuoteToken.TRANSFERABLE,
                                    finite = x.MarketEvent.QuoteToken.FINITE,
                                    divisible = x.MarketEvent.QuoteToken.DIVISIBLE,
                                    fiat = x.MarketEvent.QuoteToken.FIAT,
                                    fuel = x.MarketEvent.QuoteToken.FUEL,
                                    swappable = x.MarketEvent.QuoteToken.SWAPPABLE,
                                    burnable = x.MarketEvent.QuoteToken.BURNABLE,
                                    stakable = x.MarketEvent.QuoteToken.STAKABLE,
                                    decimals = x.MarketEvent.QuoteToken.DECIMALS
                                }
                                : null,
                            end_price = x.MarketEvent.END_PRICE,
                            price = x.MarketEvent.PRICE,
                            market_event_kind = x.MarketEvent.MarketEventKind.NAME,
                            market_id = x.MarketEvent.MARKET_ID,
                            fiat_price = with_fiat == 1
                                ? new FiatPrice
                                {
                                    fiat_currency = x.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                                    fiat_price = FiatExchangeRateMethods.Convert(fiatPricesInUsd,
                                        x.MarketEvent.MarketEventFiatPrice.PRICE_USD,
                                        x.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                                        fiatCurrency).ToString("0.####"),
                                    fiat_price_end = FiatExchangeRateMethods.Convert(fiatPricesInUsd,
                                        x.MarketEvent.MarketEventFiatPrice.PRICE_END_USD,
                                        x.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                                        fiatCurrency).ToString("0.####")
                                }
                                : null
                        }
                        : null,
                    organization_event = with_event_data == 1 && x.OrganizationEvent != null
                        ? new OrganizationEvent
                        {
                            organization = x.OrganizationEvent.Organization != null
                                ? new Organization
                                {
                                    name = x.OrganizationEvent.Organization.NAME
                                }
                                : null,
                            address = x.OrganizationEvent.Address != null
                                ? new Address
                                {
                                    address = x.OrganizationEvent.Address.ADDRESS,
                                    address_name = x.OrganizationEvent.Address.ADDRESS_NAME
                                }
                                : null
                        }
                        : null,
                    sale_event = with_event_data == 1 && x.SaleEvent != null
                        ? new SaleEvent
                        {
                            hash = x.SaleEvent.HASH,
                            sale_event_kind = x.SaleEvent.SaleEventKind.NAME
                        }
                        : null,
                    string_event = with_event_data == 1 && x.StringEvent != null
                        ? new StringEvent
                        {
                            string_value = x.StringEvent.STRING_VALUE
                        }
                        : null,
                    token_event = with_event_data == 1 && x.TokenEvent != null
                        ? new TokenEvent
                        {
                            token = x.TokenEvent.Token != null
                                ? new Token
                                {
                                    symbol = x.TokenEvent.Token.SYMBOL,
                                    fungible = x.TokenEvent.Token.FUNGIBLE,
                                    transferable = x.TokenEvent.Token.TRANSFERABLE,
                                    finite = x.TokenEvent.Token.FINITE,
                                    divisible = x.TokenEvent.Token.DIVISIBLE,
                                    fiat = x.TokenEvent.Token.FIAT,
                                    fuel = x.TokenEvent.Token.FUEL,
                                    swappable = x.TokenEvent.Token.SWAPPABLE,
                                    burnable = x.TokenEvent.Token.BURNABLE,
                                    stakable = x.TokenEvent.Token.STAKABLE,
                                    decimals = x.TokenEvent.Token.DECIMALS
                                }
                                : null,
                            value = x.TokenEvent.VALUE,
                            chain_name = x.TokenEvent.CHAIN_NAME
                        }
                        : null,
                    transaction_settle_event = with_event_data == 1 && x.TransactionSettleEvent != null
                        ? new TransactionSettleEvent
                        {
                            hash = x.TransactionSettleEvent.HASH,
                            platform = x.TransactionSettleEvent.Platform != null
                                ? new Platform
                                {
                                    name = x.TransactionSettleEvent.Platform.NAME,
                                    chain = x.TransactionSettleEvent.Platform.CHAIN,
                                    fuel = x.TransactionSettleEvent.Platform.FUEL
                                    //we do not add other information here for now
                                }
                                : null
                        }
                        : null
                }
            ).ToArray();

            #endregion

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( APIException )
        {
            throw;
        }
        catch ( Exception e )
        {
            var logMessage = LogEx.Exception("Events()", e);

            throw new APIException(logMessage, e);
        }

        return new EventsResult {total_results = with_total == 1 ? totalResults : null, events = eventsArray};
    }
}
