using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public partial class Endpoints
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Transaction Information on the backend
    /// </summary>
    /// <remarks>
    ///     <a href='#model-TransactionResult'>TransactionResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or hash</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="hash"><a href='#model-Transaction'>Transaction</a> hash</param>
    /// <param name="hash_partial"><a href='#model-Transaction'>Transaction</a> hash (partial match)</param>
    /// <param name="address">Address (Hash)</param>
    /// <param name="date_less">Date (greater than), UTC unixseconds</param>
    /// <param name="date_greater">Date (greater than), UTC unixseconds</param>
    /// <param name="block_hash"><a href='#model-Block'>Block</a> hash</param>
    /// <param name="block_height">height of the <a href='#model-Block'>Block</a></param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="with_nft" example="0">Return data with <a href='#model-NftMetadata'>nft metadata</a></param>
    /// <param name="with_events" example="0">Return event data of <a href='#model-EventsResult'>events</a></param>
    /// <param name="with_event_data" example="0">Return event data with more details, needs with_events to be set</param>
    /// <param name="with_fiat" example="0">
    ///     Return with <a href='#model-FiatPrice'>fiat_prices</a> (only
    ///     <a href='#model-MarketEvent'>market_event</a>)
    /// </param>
    /// <param name="with_script" example="0">Return with script data</param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [ProducesResponseType(typeof(TransactionResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(TransactionResult), "Returns the transaction on the backend.", false, 10, cacheTag: "transactions")]
    public TransactionResult Transactions(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string hash = "",
        string hash_partial = "",
        string address = "",
        string date_less = "",
        string date_greater = "",
        string block_hash = "",
        string block_height = "",
        string chain = "main",
        int with_nft = 0,
        int with_events = 0,
        int with_event_data = 0,
        int with_fiat = 0,
        int with_script = 0,
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        ConcurrentBag<Transaction> concurrentTransactions = new ConcurrentBag<Transaction>();

        const string fiatCurrency = "USD";
        var filter = !string.IsNullOrEmpty(hash) || !string.IsNullOrEmpty(hash_partial) ||
                     !string.IsNullOrEmpty(address) || !string.IsNullOrEmpty(date_less) ||
                     !string.IsNullOrEmpty(date_greater)
                     || !string.IsNullOrEmpty(block_hash) || !string.IsNullOrEmpty(block_height);

        try
        {
            #region ArgValidation

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, filter) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(hash) && !ArgValidation.CheckHash(hash.ToUpper()) )
                throw new ApiParameterException("Unsupported value for 'hash' parameter.");

            if ( !string.IsNullOrEmpty(hash_partial) && !ArgValidation.CheckHash(hash_partial.ToUpper()) )
                throw new ApiParameterException("Unsupported value for 'hash_partial' parameter.");

            if ( !string.IsNullOrEmpty(address) && !ArgValidation.CheckAddress(address) )
                throw new ApiParameterException("Unsupported value for 'address' parameter.");

            ContractMethods.Drop0x(ref address);

            if ( !string.IsNullOrEmpty(date_less) && !ArgValidation.CheckNumber(date_less) )
                throw new ApiParameterException("Unsupported value for 'date_less' parameter.");

            if ( !string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckNumber(date_greater) )
                throw new ApiParameterException("Unsupported value for 'date_greater' parameter.");

            if ( !string.IsNullOrEmpty(block_hash) && !ArgValidation.CheckHash(block_hash.ToUpper()) )
                throw new ApiParameterException("Unsupported value for 'block_hash' parameter.");

            if ( !string.IsNullOrEmpty(block_height) && !ArgValidation.CheckNumber(block_height) )
                throw new ApiParameterException("Unsupported value for 'block_height' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            #endregion

            var startTime = DateTime.Now;
            using MainDbContext databaseContext = new();
            var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);

            var query = databaseContext.Transactions.AsQueryable().AsNoTracking();

            #region Filtering

            if ( !string.IsNullOrEmpty(hash) )
                query = query.Where(x => x.HASH == hash.ToUpper());

            if ( !string.IsNullOrEmpty(hash_partial) )
                query = query.Where(x => x.HASH.Contains(hash_partial.ToUpper()));

            if ( !string.IsNullOrEmpty(date_less) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

            if ( !string.IsNullOrEmpty(date_greater) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

            if ( !string.IsNullOrEmpty(address) )
            {
                bool isValidAddress = Phantasma.Core.Cryptography.Address.IsValidAddress(address);
                var addressTransactions = AddressTransactionMethods
                    .GetAddressTransactionsByAddress(databaseContext, address, isValidAddress).ToList();
                
                query = query.Where(x => x.AddressTransactions.Any(y => addressTransactions.Contains(y)));
            }

            if ( !string.IsNullOrEmpty(block_hash) )
                query = query.Where(x => x.Block.HASH == block_hash.ToUpper());

            if ( !string.IsNullOrEmpty(block_height) )
                query = query.Where(x => x.Block.HEIGHT == block_height);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Block.Chain.NAME == chain);

            #endregion

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

            if ( limit > 0 ) query = query.Skip(offset).Take(limit);

            Parallel.ForEach(query, _transaction =>
            {
                // Perform your processing here
                Transaction _tx = ProcessTransaction(_transaction, with_script, with_events, with_event_data, with_nft, with_fiat, fiatCurrency, fiatPricesInUsd);

                // Add the result to the ConcurrentBag
                concurrentTransactions.Add(_tx);
            });

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Transaction()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new TransactionResult
            {total_results = with_total == 1 ? totalResults : null, transactions = concurrentTransactions.ToArray()};
    }


    private Transaction ProcessTransaction(Database.Main.Transaction transaction, int with_script, int with_events, int with_event_data, int with_nft, int with_fiat, string fiatCurrency, Dictionary<string, decimal> fiatPricesInUsd)
    {
        return new Transaction
        {
            hash = transaction.HASH,
            block_hash = transaction.Block.HASH,
            block_height = transaction.Block.HEIGHT,
            index = transaction.INDEX,
            date = transaction.TIMESTAMP_UNIX_SECONDS.ToString(),
            fee = transaction.FEE,
            fee_raw = transaction.FEE_RAW,
            script_raw = with_script == 1 ? transaction.SCRIPT_RAW : null,
            result = transaction.RESULT,
            payload = transaction.PAYLOAD,
            expiration = transaction.EXPIRATION.ToString(),
            gas_price = transaction.GAS_PRICE,
            gas_price_raw = transaction.GAS_PRICE_RAW,
            gas_limit = transaction.GAS_LIMIT,
            gas_limit_raw = transaction.GAS_LIMIT_RAW,
            state = transaction.State.NAME,
            sender = transaction.Sender != null
                ? new Address
                {
                    address_name = transaction.Sender.ADDRESS_NAME,
                    address = transaction.Sender.ADDRESS
                }
                : null,
            gas_payer = transaction.GasPayer != null
                ? new Address
                {
                    address_name = transaction.GasPayer.ADDRESS_NAME,
                    address = transaction.GasPayer.ADDRESS
                }
                : null,
            gas_target = transaction.GasTarget != null
                ? new Address
                {
                    address_name = transaction.GasTarget.ADDRESS_NAME,
                    address = transaction.GasTarget.ADDRESS
                }
                : null,
            events = HandleEvents(transaction, with_events, with_event_data, with_nft, with_fiat, fiatCurrency,
                fiatPricesInUsd)
        };
    }


    private Event[] HandleEvents(Database.Main.Transaction transaction, int with_events, int with_event_data, int with_nft, int with_fiat, string fiatCurrency, Dictionary<string, decimal> fiatPricesInUsd)
    {
        if ( with_events != 1 ) return null;
        if ( transaction.Events == null ) return null;
        if ( transaction.Events.Count == 0 ) return null;
        //if ( transaction.Events.Count > 100 ) throw new ApiParameterException("Too many events in transaction.");
        ConcurrentBag<Event> resultBag = new ConcurrentBag<Event>();

        Parallel.ForEach(transaction.Events, _transactionEvent =>
        {
            // Perform your processing here
            Event _event = ProcessEvent(_transactionEvent, transaction, with_event_data, with_nft, with_fiat, fiatCurrency, fiatPricesInUsd);

            // Add the result to the ConcurrentBag
            resultBag.Add(_event);
        });

        return resultBag.ToArray();
    }


    private Event ProcessEvent(Database.Main.Event _transactionEvent, Database.Main.Transaction transaction, int with_event_data, int with_nft, int with_fiat, string fiatCurrency, Dictionary<string, decimal> fiatPricesInUsd)
    {
        Event _event = new Event();
        _event.event_id = _transactionEvent.ID;
        _event.chain = _transactionEvent.Chain.NAME.ToLower();
        _event.date = _transactionEvent.TIMESTAMP_UNIX_SECONDS.ToString();
        _event.transaction_hash = transaction.HASH; //a bit redundant in that case
        _event.token_id = _transactionEvent.TOKEN_ID;
        _event.event_kind = _transactionEvent.EventKind.NAME;
        _event.address = _transactionEvent.Address.ADDRESS;
        _event.address_name = _transactionEvent.Address.ADDRESS_NAME;
        _event.contract = new Contract
        {
            name = _transactionEvent.Contract.NAME,
            hash = ContractMethods.Prepend0x(_transactionEvent.Contract.HASH, _transactionEvent.Chain.NAME),
            symbol = _transactionEvent.Contract.SYMBOL
        };
        _event.nft_metadata = with_nft == 1 && _transactionEvent.Nft != null
            ? new NftMetadata
            {
                name = _transactionEvent.Nft.NAME,
                description = _transactionEvent.Nft.DESCRIPTION,
                image = _transactionEvent.Nft.IMAGE,
                video = _transactionEvent.Nft.VIDEO,
                rom = _transactionEvent.Nft.ROM,
                ram = _transactionEvent.Nft.RAM,
                mint_date = _transactionEvent.Nft.MINT_DATE_UNIX_SECONDS.ToString(),
                mint_number = _transactionEvent.Nft.MINT_NUMBER.ToString(),
            }
            : null;
        _event.series = with_nft == 1 && _transactionEvent.Nft != null && _transactionEvent.Nft.Series != null
            ? new Series
            {
                id = _transactionEvent.Nft.Series.ID,
                series_id = _transactionEvent.Nft.Series.SERIES_ID,
                creator = _transactionEvent.Nft.Series.CreatorAddress != null
                    ? _transactionEvent.Nft.Series.CreatorAddress.ADDRESS
                    : null,
                current_supply = _transactionEvent.Nft.Series.CURRENT_SUPPLY,
                max_supply = _transactionEvent.Nft.Series.MAX_SUPPLY,
                mode_name = _transactionEvent.Nft.Series.SeriesMode != null
                    ? _transactionEvent.Nft.Series.SeriesMode.MODE_NAME
                    : null,
                name = _transactionEvent.Nft.Series.NAME,
                description = _transactionEvent.Nft.Series.DESCRIPTION,
                image = _transactionEvent.Nft.Series.IMAGE,
                royalties = _transactionEvent.Nft.Series.ROYALTIES.ToString(CultureInfo.InvariantCulture),
                type = _transactionEvent.Nft.Series.TYPE,
                attr_type_1 = _transactionEvent.Nft.Series.ATTR_TYPE_1,
                attr_value_1 = _transactionEvent.Nft.Series.ATTR_VALUE_1,
                attr_type_2 = _transactionEvent.Nft.Series.ATTR_TYPE_2,
                attr_value_2 = _transactionEvent.Nft.Series.ATTR_VALUE_2,
                attr_type_3 = _transactionEvent.Nft.Series.ATTR_TYPE_3,
                attr_value_3 = _transactionEvent.Nft.Series.ATTR_VALUE_3
            }
            : null;
        _event.address_event = with_event_data == 1 && _transactionEvent.AddressEvent != null
            ? new AddressEvent
            {
                address = _transactionEvent.AddressEvent.Address != null
                    ? new Address
                    {
                        address_name = _transactionEvent.AddressEvent.Address.ADDRESS_NAME,
                        address = _transactionEvent.AddressEvent.Address.ADDRESS
                    }
                    : null
            }
            : null;
        _event.chain_event = with_event_data == 1 && _transactionEvent.ChainEvent != null
            ? new ChainEvent
            {
                name = _transactionEvent.ChainEvent.NAME,
                value = _transactionEvent.ChainEvent.VALUE,
                chain = _transactionEvent.ChainEvent.Chain != null
                    ? new Chain
                    {
                        chain_name = _transactionEvent.ChainEvent.Chain.NAME
                    }
                    : null
            }
            : null;
        _event.gas_event = with_event_data == 1 && _transactionEvent.GasEvent != null
            ? new GasEvent
            {
                price = _transactionEvent.GasEvent.PRICE,
                amount = _transactionEvent.GasEvent.AMOUNT,
                fee = _transactionEvent.GasEvent.FEE,
                address = _transactionEvent.GasEvent.Address != null
                    ? new Address
                    {
                        address = _transactionEvent.GasEvent.Address.ADDRESS,
                        address_name = _transactionEvent.GasEvent.Address.ADDRESS_NAME
                    }
                    : null
            }
            : null;
        _event.hash_event = with_event_data == 1 && _transactionEvent.HashEvent != null
            ? new HashEvent
            {
                hash = _transactionEvent.HashEvent.HASH
            }
            : null;
        _event.infusion_event = with_event_data == 1 && _transactionEvent.InfusionEvent != null
            ? new InfusionEvent
            {
                token_id = _transactionEvent.InfusionEvent.TOKEN_ID,
                infused_value = _transactionEvent.InfusionEvent.INFUSED_VALUE,
                infused_value_raw = _transactionEvent.InfusionEvent.INFUSED_VALUE_RAW,
                base_token = _transactionEvent.InfusionEvent.BaseToken != null
                    ? new Token
                    {
                        symbol = _transactionEvent.InfusionEvent.BaseToken.SYMBOL,
                        fungible = _transactionEvent.InfusionEvent.BaseToken.FUNGIBLE,
                        transferable = _transactionEvent.InfusionEvent.BaseToken.TRANSFERABLE,
                        finite = _transactionEvent.InfusionEvent.BaseToken.FINITE,
                        divisible = _transactionEvent.InfusionEvent.BaseToken.DIVISIBLE,
                        fiat = _transactionEvent.InfusionEvent.BaseToken.FIAT,
                        fuel = _transactionEvent.InfusionEvent.BaseToken.FUEL,
                        swappable = _transactionEvent.InfusionEvent.BaseToken.SWAPPABLE,
                        burnable = _transactionEvent.InfusionEvent.BaseToken.BURNABLE,
                        stakable = _transactionEvent.InfusionEvent.BaseToken.STAKABLE,
                        decimals = _transactionEvent.InfusionEvent.BaseToken.DECIMALS
                    }
                    : null,
                infused_token = _transactionEvent.InfusionEvent.InfusedToken != null
                    ? new Token
                    {
                        symbol = _transactionEvent.InfusionEvent.InfusedToken.SYMBOL,
                        fungible = _transactionEvent.InfusionEvent.InfusedToken.FUNGIBLE,
                        transferable = _transactionEvent.InfusionEvent.InfusedToken.TRANSFERABLE,
                        finite = _transactionEvent.InfusionEvent.InfusedToken.FINITE,
                        divisible = _transactionEvent.InfusionEvent.InfusedToken.DIVISIBLE,
                        fiat = _transactionEvent.InfusionEvent.InfusedToken.FIAT,
                        fuel = _transactionEvent.InfusionEvent.InfusedToken.FUEL,
                        swappable = _transactionEvent.InfusionEvent.InfusedToken.SWAPPABLE,
                        burnable = _transactionEvent.InfusionEvent.InfusedToken.BURNABLE,
                        stakable = _transactionEvent.InfusionEvent.InfusedToken.STAKABLE,
                        decimals = _transactionEvent.InfusionEvent.InfusedToken.DECIMALS
                    }
                    : null
            }
            : null;
        _event.market_event = with_event_data == 1 && _transactionEvent.MarketEvent != null
            ? new MarketEvent
            {
                base_token = _transactionEvent.MarketEvent.BaseToken != null
                    ? new Token
                    {
                        symbol = _transactionEvent.MarketEvent.BaseToken.SYMBOL,
                        fungible = _transactionEvent.MarketEvent.BaseToken.FUNGIBLE,
                        transferable = _transactionEvent.MarketEvent.BaseToken.TRANSFERABLE,
                        finite = _transactionEvent.MarketEvent.BaseToken.FINITE,
                        divisible = _transactionEvent.MarketEvent.BaseToken.DIVISIBLE,
                        fiat = _transactionEvent.MarketEvent.BaseToken.FIAT,
                        fuel = _transactionEvent.MarketEvent.BaseToken.FUEL,
                        swappable = _transactionEvent.MarketEvent.BaseToken.SWAPPABLE,
                        burnable = _transactionEvent.MarketEvent.BaseToken.BURNABLE,
                        stakable = _transactionEvent.MarketEvent.BaseToken.STAKABLE,
                        decimals = _transactionEvent.MarketEvent.BaseToken.DECIMALS
                    }
                    : null,
                quote_token = _transactionEvent.MarketEvent.QuoteToken != null
                    ? new Token
                    {
                        symbol = _transactionEvent.MarketEvent.QuoteToken.SYMBOL,
                        fungible = _transactionEvent.MarketEvent.QuoteToken.FUNGIBLE,
                        transferable = _transactionEvent.MarketEvent.QuoteToken.TRANSFERABLE,
                        finite = _transactionEvent.MarketEvent.QuoteToken.FINITE,
                        divisible = _transactionEvent.MarketEvent.QuoteToken.DIVISIBLE,
                        fiat = _transactionEvent.MarketEvent.QuoteToken.FIAT,
                        fuel = _transactionEvent.MarketEvent.QuoteToken.FUEL,
                        swappable = _transactionEvent.MarketEvent.QuoteToken.SWAPPABLE,
                        burnable = _transactionEvent.MarketEvent.QuoteToken.BURNABLE,
                        stakable = _transactionEvent.MarketEvent.QuoteToken.STAKABLE,
                        decimals = _transactionEvent.MarketEvent.QuoteToken.DECIMALS
                    }
                    : null,
                end_price = _transactionEvent.MarketEvent.END_PRICE,
                price = _transactionEvent.MarketEvent.PRICE,
                market_event_kind = _transactionEvent.MarketEvent.MarketEventKind.NAME,
                market_id = _transactionEvent.MarketEvent.MARKET_ID,
                fiat_price = with_fiat == 1
                    ? new FiatPrice
                    {
                        fiat_currency =
                            _transactionEvent.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                        fiat_price = FiatExchangeRateMethods.Convert(fiatPricesInUsd,
                            _transactionEvent.MarketEvent.MarketEventFiatPrice.PRICE_USD,
                            _transactionEvent.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                            fiatCurrency).ToString("0.####"),
                        fiat_price_end = FiatExchangeRateMethods.Convert(fiatPricesInUsd,
                            _transactionEvent.MarketEvent.MarketEventFiatPrice.PRICE_END_USD,
                            _transactionEvent.MarketEvent.MarketEventFiatPrice.FIAT_NAME,
                            fiatCurrency).ToString("0.####")
                    }
                    : null
            }
            : null;
        _event.organization_event = with_event_data == 1 && _transactionEvent.OrganizationEvent != null
            ? new OrganizationEvent
            {
                organization = _transactionEvent.OrganizationEvent.Organization != null
                    ? new Organization
                    {
                        name = _transactionEvent.OrganizationEvent.Organization.NAME
                    }
                    : null,
                address = _transactionEvent.OrganizationEvent.Address != null
                    ? new Address
                    {
                        address = _transactionEvent.OrganizationEvent.Address.ADDRESS,
                        address_name = _transactionEvent.OrganizationEvent.Address.ADDRESS_NAME
                    }
                    : null
            }
            : null;
        _event.sale_event = with_event_data == 1 && _transactionEvent.SaleEvent != null
            ? new SaleEvent
            {
                hash = _transactionEvent.SaleEvent.HASH,
                sale_event_kind = _transactionEvent.SaleEvent.SaleEventKind.NAME
            }
            : null;
        _event.string_event = with_event_data == 1 && _transactionEvent.StringEvent != null
            ? new StringEvent
            {
                string_value = _transactionEvent.StringEvent.STRING_VALUE
            }
            : null;
        _event.token_event = with_event_data == 1 && _transactionEvent.TokenEvent != null
            ? new TokenEvent
            {
                token = _transactionEvent.TokenEvent.Token != null
                    ? new Token
                    {
                        symbol = _transactionEvent.TokenEvent.Token.SYMBOL,
                        fungible = _transactionEvent.TokenEvent.Token.FUNGIBLE,
                        transferable = _transactionEvent.TokenEvent.Token.TRANSFERABLE,
                        finite = _transactionEvent.TokenEvent.Token.FINITE,
                        divisible = _transactionEvent.TokenEvent.Token.DIVISIBLE,
                        fiat = _transactionEvent.TokenEvent.Token.FIAT,
                        fuel = _transactionEvent.TokenEvent.Token.FUEL,
                        swappable = _transactionEvent.TokenEvent.Token.SWAPPABLE,
                        burnable = _transactionEvent.TokenEvent.Token.BURNABLE,
                        stakable = _transactionEvent.TokenEvent.Token.STAKABLE,
                        decimals = _transactionEvent.TokenEvent.Token.DECIMALS
                    }
                    : null,
                value = _transactionEvent.TokenEvent.VALUE,
                value_raw = _transactionEvent.TokenEvent.VALUE_RAW,
                chain_name = _transactionEvent.TokenEvent.CHAIN_NAME
            }
            : null;
        _event.transaction_settle_event = with_event_data == 1 && _transactionEvent.TransactionSettleEvent != null
            ? new TransactionSettleEvent
            {
                hash = _transactionEvent.TransactionSettleEvent.HASH,
                platform = _transactionEvent.TransactionSettleEvent.Platform != null
                    ? new Platform
                    {
                        name = _transactionEvent.TransactionSettleEvent.Platform.NAME,
                        chain = _transactionEvent.TransactionSettleEvent.Platform.CHAIN,
                        fuel = _transactionEvent.TransactionSettleEvent.Platform.FUEL
                        //we do not add other information here for now
                    }
                    : null
            }
            : null;

        return _event;
    }
}
