using System;
using System.Globalization;
using System.Linq;
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
using Transaction = GhostDevs.Service.ApiResults.Transaction;
using TransactionSettleEvent = GhostDevs.Service.ApiResults.TransactionSettleEvent;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(TransactionResult), "Returns the transaction on the backend.", false, 10)]
    public TransactionResult Transactions(
        [APIParameter("Order by [id, hash]", "string")]
        string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("hash", "string")] string hash = "",
        [APIParameter("hash (partial match)", "string")]
        string hash_partial = "",
        [APIParameter("Address", "string")] string address = "",
        [APIParameter("Date (less than)", "string")]
        string date_less = "",
        [APIParameter("Date (greater than)", "string")]
        string date_greater = "",
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0,
        [APIParameter("Return data with nft metadata", "integer")]
        int with_nft = 0,
        [APIParameter("Return data with events of the transaction", "integer")]
        int with_events = 0,
        [APIParameter("Return event data of events", "integer")]
        int with_event_data = 0,
        [APIParameter("Return with fiat_prices (only at market_event)", "integer")]
        int with_fiat = 0
    )
    {
        long totalResults = 0;
        Transaction[] transactionArray;
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

                if ( !string.IsNullOrEmpty(address) && !ArgValidation.CheckAddress(address) )
                    throw new APIException("Unsupported value for 'address' parameter.");

                ContractMethods.Drop0x(ref address);

                if ( !string.IsNullOrEmpty(date_less) && !ArgValidation.CheckDateString(date_less) )
                    throw new APIException("Unsupported value for 'date_less' parameter.");

                if ( !string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckDateString(date_greater) )
                    throw new APIException("Unsupported value for 'date_greater' parameter.");

                var startTime = DateTime.Now;

                var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);

                var query = databaseContext.Transactions.AsQueryable();


                if ( !string.IsNullOrEmpty(hash) )
                    query = query.Where(x => string.Equals(x.HASH.ToUpper(), hash.ToUpper()));

                if ( !string.IsNullOrEmpty(hash_partial) )
                    query = query.Where(x => x.HASH.ToUpper().Contains(hash_partial.ToUpper()));

                if ( !string.IsNullOrEmpty(date_less) )
                    query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

                if ( !string.IsNullOrEmpty(date_greater) )
                    query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

                if ( !string.IsNullOrEmpty(address) )
                {
                    var addressTransactions = AddressTransactionMethods
                        .GetAddressTransactionsByAddress(databaseContext, address).ToList();
                    query = query.Where(x => x.AddressTransactions.Any(y => addressTransactions.Contains(y)));
                }

                // Count total number of results before adding order and limit parts of query.
                if ( with_total == 1 )
                    totalResults = query.Count();

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


                transactionArray = ( from x in query.Skip(offset).Take(limit)
                    let events = x.Events
                    select new Transaction
                    {
                        hash = x.HASH,
                        blockHeight = x.Block.HEIGHT,
                        index = x.INDEX,
                        date = x.TIMESTAMP_UNIX_SECONDS.ToString(),
                        events = with_events == 1 && x.Events != null
                            ? events.Select(e => new Event
                            {
                                chain = e.Chain.NAME.ToLower(),
                                date = e.TIMESTAMP_UNIX_SECONDS.ToString(),
                                transaction_hash = x.HASH, //a bit redundant in that case
                                token_id = e.TOKEN_ID,
                                event_kind = e.EventKind.NAME,
                                address = e.Address.ADDRESS,
                                address_name = e.Address.ADDRESS_NAME,
                                contract = new Contract
                                {
                                    name = e.Contract.NAME,
                                    hash = ContractMethods.Prepend0x(e.Contract.HASH, e.Chain.NAME),
                                    symbol = e.Contract.SYMBOL
                                },
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
                                series = with_nft == 1 && ( e.Nft != null && e.Nft.Series != null )
                                    ? new Series
                                    {
                                        id = e.Nft.Series.SERIES_ID,
                                        creator = e.Nft.Series.CreatorAddress != null
                                            ? e.Nft.Series.CreatorAddress.ADDRESS
                                            : null,
                                        current_supply = e.Nft.Series.CURRENT_SUPPLY,
                                        max_supply = e.Nft.Series.MAX_SUPPLY,
                                        mode_name = ( e.Nft.Series.SeriesMode != null
                                            ? e.Nft.Series.SeriesMode.MODE_NAME
                                            : null )!,
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
                                            }
                                            : null,
                                        end_price = e.MarketEvent.END_PRICE,
                                        price = e.MarketEvent.PRICE,
                                        market_event_kind = e.MarketEvent.MarketEventKind.NAME,
                                        market_id = e.MarketEvent.MARKET_ID,
                                        fiat_price = with_fiat == 1
                                            ? new FiatPrice
                                            {
                                                fiat_currency = e.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                                                fiat_price = FiatExchangeRateMethods.Convert(fiatPricesInUsd,
                                                    e.MarketEvent.MarketEventFiatPrice.PRICE_USD,
                                                    e.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                                                    fiatCurrency).ToString("0.####"),
                                                fiat_price_end = FiatExchangeRateMethods.Convert(fiatPricesInUsd,
                                                    e.MarketEvent.MarketEventFiatPrice.PRICE_END_USD,
                                                    e.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                                                    fiatCurrency).ToString("0.####")
                                            }
                                            : null
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
                            : null
                    } ).ToArray();

                var responseTime = DateTime.Now - startTime;

                Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
            }
            catch ( APIException )
            {
                throw;
            }
            catch ( Exception exception )
            {
                var logMessage = LogEx.Exception("Transaction()", exception);

                throw new APIException(logMessage, exception);
            }
        }

        return new TransactionResult
            {total_results = with_total == 1 ? totalResults : null, transactions = transactionArray};
    }
}
