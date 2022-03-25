using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
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
    [APIInfo(typeof(EventsResult), "Returns events available on the backend.", false, 10)]
    public EventsResult Events([APIParameter("Order by [date, token_id]", "string")] string order_by = "date",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("Chain name (ex. 'main')", "string")]
        string chain = "",
        [APIParameter("Token contract hash", "string")]
        string contract = "",
        [APIParameter("Token ID", "string")] string token_id = "",
        [APIParameter("Date day match (matches whole given day)", "string")]
        string date_day = "",
        [APIParameter("Date (less than)", "string")]
        string date_less = "",
        [APIParameter("Date (greater than)", "string")]
        string date_greater = "",
        [APIParameter("Event kind", "string")] string event_kind = "",
        [APIParameter("Event kind (parial match)", "string")]
        string event_kind_partial = "",
        [APIParameter("Nft name (parial match)", "string")]
        string nft_name_partial = "",
        [APIParameter("Nft description (parial match)", "string")]
        string nft_description_partial = "",
        [APIParameter("Address", "string")] string address = "",
        [APIParameter("Address (partial match)", "string")]
        string address_partial = "",
        [APIParameter("Return event data of events", "integer")]
        int with_event_data = 0,
        [APIParameter("Return NFT metadata with events", "integer")]
        int with_metadata = 0,
        [APIParameter("Return NFT series with events", "integer")]
        int with_series = 0,
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0,
        [APIParameter("Return with fiat_prices (only at market_event)", "integer")]
        int with_fiat = 0,
        [APIParameter("Return with events marked nsfw as well", "integer")]
        int with_nsfw = 0,
        [APIParameter("Return blacklisted events as well", "integer")]
        int with_blacklisted = 0)
    {
        // Results of the query
        long totalResults = 0;
        Event[] eventsArray;
        const string fiatCurrency = "USD";


        using ( var databaseContext = new MainDbContext() )
        {
            try
            {
                if ( !ArgValidation.CheckLimit(limit) )
                    throw new APIException("Unsupported value for 'limit' parameter.");

                if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                    throw new APIException("Unsupported value for 'order_by' parameter.");

                if ( !ArgValidation.CheckOrderDirection(order_direction) )
                    throw new APIException("Unsupported value for 'order_direction' parameter.");

                if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                    throw new APIException("Unsupported value for 'chain' parameter.");

                if ( !string.IsNullOrEmpty(contract) && !ArgValidation.CheckAddress(contract) )
                    throw new APIException("Unsupported value for 'contract' parameter.");

                ContractMethods.Drop0x(ref contract);

                if ( !string.IsNullOrEmpty(token_id) && !Regex.IsMatch(token_id, @"^[_\-a-zA-Z0-9]+$") )
                    throw new APIException("Unsupported value for 'token_id' parameter.");

                if ( !string.IsNullOrEmpty(date_day) && !Regex.IsMatch(date_day, @"^[0-9.]+$") )
                    throw new APIException("Unsupported value for 'date_day' parameter.");

                if ( !string.IsNullOrEmpty(date_less) && !ArgValidation.CheckDateString(date_less) )
                    throw new APIException("Unsupported value for 'date_less' parameter.");

                if ( !string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckDateString(date_greater) )
                    throw new APIException("Unsupported value for 'date_greater' parameter.");

                if ( !string.IsNullOrEmpty(event_kind) && !Regex.IsMatch(event_kind, @"^[_\-a-zA-Z0-9]+$") )
                    throw new APIException("Unsupported value for 'event_kind' parameter.");

                if ( !string.IsNullOrEmpty(event_kind_partial) &&
                     !Regex.IsMatch(event_kind_partial, @"^[_\-a-zA-Z0-9]+$") )
                    throw new APIException("Unsupported value for 'event_kind_partial' parameter.");

                if ( !string.IsNullOrEmpty(nft_name_partial) && !ArgValidation.CheckName(nft_name_partial) )
                    throw new APIException("Unsupported value for 'nft_name_partial' parameter.");

                if ( !string.IsNullOrEmpty(nft_description_partial) &&
                     !Regex.IsMatch(nft_description_partial, @"^[_\-a-zA-Z0-9]+$") )
                    throw new APIException("Unsupported value for 'nft_description_partial' parameter.");

                if ( !string.IsNullOrEmpty(address) && !ArgValidation.CheckAddress(address) )
                    throw new APIException("Unsupported value for 'address' parameter.");

                ContractMethods.Drop0x(ref address);

                if ( !string.IsNullOrEmpty(address_partial) && !ArgValidation.CheckAddress(address_partial) )
                    throw new APIException("Unsupported value for 'address_partial' parameter.");

                ContractMethods.Drop0x(ref address_partial);

                var startTime = DateTime.Now;
                var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);

                // Getting exchange rates in advance.
                var query = databaseContext.Events.AsQueryable();

                if ( with_nsfw == 0 )
                    query = query.Where(x => x.NSFW != true);

                if ( with_blacklisted == 0 )
                    query = query.Where(x => x.BLACKLISTED != true);

                if ( !string.IsNullOrEmpty(chain) )
                    query = query.Where(x => string.Equals(x.Chain.NAME.ToUpper(), chain.ToUpper()));

                if ( !string.IsNullOrEmpty(token_id) ) query = query.Where(x => x.TOKEN_ID == token_id);

                if ( !string.IsNullOrEmpty(contract) )
                    query = query.Where(x => string.Equals(x.Contract.HASH.ToUpper(), contract.ToUpper()));

                if ( !string.IsNullOrEmpty(date_day) )
                    query = query.Where(x => x.DATE_UNIX_SECONDS == UnixSeconds.FromDateTimeString(date_day));

                if ( !string.IsNullOrEmpty(date_less) )
                    query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

                if ( !string.IsNullOrEmpty(date_greater) )
                    query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

                if ( !string.IsNullOrEmpty(event_kind) )
                    query = query.Where(x => string.Equals(x.EventKind.NAME.ToUpper(), event_kind.ToUpper()));

                if ( !string.IsNullOrEmpty(event_kind_partial) )
                    query = query.Where(x => x.EventKind.NAME.ToUpper().Contains(event_kind_partial.ToUpper()));

                if ( !string.IsNullOrEmpty(nft_name_partial) )
                    query = query.Where(x => x.Nft.NAME.ToUpper().Contains(nft_name_partial.ToUpper()));

                if ( !string.IsNullOrEmpty(nft_description_partial) )
                    query = query.Where(x => x.Nft.DESCRIPTION.ToUpper().Contains(nft_description_partial.ToUpper()));

                if ( !string.IsNullOrEmpty(address) )
                {
                    var ids = AddressMethods.GetIdsFromExtendedFormat(databaseContext, address, true, chain);

                    query = query.Where(x => ids.Contains(x.AddressId)
                    );
                }

                if ( !string.IsNullOrEmpty(address_partial) )
                    query = query.Where(x => x.Address.ADDRESS.Contains(address_partial) ||
                                             x.Address.ADDRESS_NAME.ToUpper().Contains(address_partial.ToUpper()) ||
                                             x.Address.USER_NAME.ToUpper().Contains(address_partial.ToUpper()));

                if ( order_direction == "asc" )
                    query = order_by switch
                    {
                        "date" => query.OrderBy(x => x.TIMESTAMP_UNIX_SECONDS)
                            .ThenBy(x => x.Transaction.INDEX)
                            .ThenBy(x => x.INDEX),
                        "token_id" => query.OrderBy(x => x.TOKEN_ID),
                        _ => query
                    };
                else
                    query = order_by switch
                    {
                        "date" => query.OrderByDescending(x => x.TIMESTAMP_UNIX_SECONDS)
                            .ThenByDescending(x => x.Transaction.INDEX)
                            .ThenByDescending(x => x.INDEX),
                        "token_id" => query.OrderByDescending(x => x.TOKEN_ID),
                        _ => query
                    };

                if ( with_total == 1 )
                    // Count total number of results before adding order and limit parts of query.
                    totalResults = query.Count();

                eventsArray = query.Skip(offset).Take(limit).Select(x => new Event
                    {
                        chain = x.Chain.NAME.ToLower(),
                        date = x.TIMESTAMP_UNIX_SECONDS.ToString(),
                        transaction_hash = x.Transaction == null ? null : x.Transaction.HASH,
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
                                // rom = x.Nft.ROM,
                                // ram = x.Nft.RAM,
                                mint_date = x.Nft.MINT_DATE_UNIX_SECONDS.ToString(),
                                mint_number = x.Nft.MINT_NUMBER.ToString()
                            }
                            : null,
                        series = with_series == 1 && x.Nft != null && x.Nft.Series != null
                            ? new Series
                            {
                                id = x.Nft.Series.SERIES_ID,
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
                                attrType1 = x.Nft.Series.ATTR_TYPE_1,
                                attrValue1 = x.Nft.Series.ATTR_VALUE_1,
                                attrType2 = x.Nft.Series.ATTR_TYPE_2,
                                attrValue2 = x.Nft.Series.ATTR_VALUE_2,
                                attrType3 = x.Nft.Series.ATTR_TYPE_3,
                                attrValue3 = x.Nft.Series.ATTR_VALUE_3
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
                                        name = x.OrganizationEvent.Organization.NAME,
                                        addresses = null //atm we do not include them here
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
        }

        return new EventsResult {total_results = with_total == 1 ? totalResults : null, events = eventsArray};
    }
}
