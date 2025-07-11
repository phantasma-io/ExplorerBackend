using System;
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

public static class GetTransactions
{
    [ProducesResponseType(typeof(TransactionResult), ( int )HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(TransactionResult), "Returns the transaction on the backend.", false, 60, cacheTag: "transactions")]
    public static async Task<TransactionResult> Execute(
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
        Transaction[] transactions = null;

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
            await using MainDbContext databaseContext = new();
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
                var addressId = 0;
                if ( PhantasmaPhoenix.Cryptography.Address.IsValidAddress(address) )
                {
                    addressId = await databaseContext.Addresses.Where(x => x.ADDRESS == address).Select(x => x.ID).FirstOrDefaultAsync();
                }
                else
                {
                    addressId = await databaseContext.Addresses.Where(x => x.ADDRESS_NAME == address).Select(x => x.ID).FirstOrDefaultAsync();
                }
                
                query = query.Where(x => x.TransactionAddresses.Any(y => y.AddressId == addressId));
            }

            if ( !string.IsNullOrEmpty(block_hash) )
                query = query.Where(x => x.Block.HASH == block_hash.ToUpper());

            if ( !string.IsNullOrEmpty(block_height) )
                query = query.Where(x => x.Block.HEIGHT == block_height);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Block.Chain.NAME == chain);

            #endregion

            // Count total number of results before adding order and limit parts of query.
            if ( with_total == 1 )
                totalResults = await query.CountAsync();

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

            if ( !String.IsNullOrEmpty(hash) )
            {
                query = query.Take(1);
            }else if ( limit > 0 ) query = query.Skip(offset).Take(limit);

            var select = query.Select(x => new Transaction
                {
                    hash = x.HASH,
                    block_hash = x.Block.HASH,
                    block_height = x.Block.HEIGHT,
                    index = x.INDEX,
                    date = x.TIMESTAMP_UNIX_SECONDS.ToString(),
                    fee = x.FEE,
                    fee_raw = x.FEE_RAW,
                    script_raw = with_script == 1 ? x.SCRIPT_RAW : null,
                    result = x.RESULT,
                    payload = x.PAYLOAD,
                    expiration = x.EXPIRATION.ToString(),
                    gas_price = x.GAS_PRICE,
                    gas_price_raw = x.GAS_PRICE_RAW,
                    gas_limit = x.GAS_LIMIT,
                    gas_limit_raw = x.GAS_LIMIT_RAW,
                    state = x.State != null ? x.State.NAME : null,
                    sender = x.Sender != null
                        ? new Address
                        {
                            address_name = x.Sender.ADDRESS_NAME,
                            address = x.Sender.ADDRESS
                        }
                        : null,
                    gas_payer = x.GasPayer != null
                        ? new Address
                        {
                            address_name = x.GasPayer.ADDRESS_NAME,
                            address = x.GasPayer.ADDRESS
                        }
                        : null,
                    gas_target = x.GasTarget != null
                        ? new Address
                        {
                            address_name = x.GasTarget.ADDRESS_NAME,
                            address = x.GasTarget.ADDRESS
                        }
                        : null,
                    events = with_events == 1
                        ? x.Events.Select(e =>
                            new Event
                            {
                                event_id = e.ID,
                                chain = e.Chain.NAME.ToLower(),
                                date = e.TIMESTAMP_UNIX_SECONDS.ToString(),
                                transaction_hash = x.HASH,
                                token_id = e.TOKEN_ID,
                                event_kind = e.EventKind.NAME,
                                address = e.Address.ADDRESS,
                                address_name = e.Address.ADDRESS_NAME,
                                contract = e.Contract != null
                                    ? new Contract
                                    {
                                        name = e.Contract.NAME,
                                        hash = e.Contract.HASH,
                                        symbol = e.Contract.SYMBOL
                                    }
                                    : null,
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
                                series = with_nft == 1 && e.Nft != null && e.Nft.Series != null
                                    ? new Series
                                    {
                                        id = e.Nft.Series.ID,
                                        series_id = e.Nft.Series.SERIES_ID,
                                        creator = e.Nft.Series.CreatorAddress != null
                                            ? e.Nft.Series.CreatorAddress.ADDRESS
                                            : null,
                                        current_supply = e.Nft.Series.CURRENT_SUPPLY,
                                        max_supply = e.Nft.Series.MAX_SUPPLY,
                                        mode_name = e.Nft.Series.SeriesMode != null
                                            ? e.Nft.Series.SeriesMode.MODE_NAME
                                            : null,
                                        name = e.Nft.Series.NAME,
                                        description = e.Nft.Series.DESCRIPTION,
                                        image = e.Nft.Series.IMAGE,
                                        royalties = e.Nft.Series.ROYALTIES.ToString(CultureInfo.InvariantCulture),
                                        type = e.Nft.Series.TYPE,
                                        attr_type_1 = e.Nft.Series.ATTR_TYPE_1,
                                        attr_value_1 = e.Nft.Series.ATTR_VALUE_1,
                                        attr_type_2 = e.Nft.Series.ATTR_TYPE_2,
                                        attr_value_2 = e.Nft.Series.ATTR_VALUE_2,
                                        attr_type_3 = e.Nft.Series.ATTR_TYPE_3,
                                        attr_value_3 = e.Nft.Series.ATTR_VALUE_3
                                    }
                                    : null,
                                address_event = with_event_data == 1 && e.TargetAddress != null
                                    ? new AddressEvent
                                    {
                                        address = new Address
                                        {
                                            address_name = e.TargetAddress.ADDRESS_NAME,
                                            address = e.TargetAddress.ADDRESS
                                        }
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
                                                chain_name = e.Chain.NAME.ToLower()
                                            }
                                            : null
                                    }
                                    : null,
                                gas_event = with_event_data == 1 && e.GasEvent != null
                                    ? new GasEvent
                                    {
                                        price = e.GasEvent.PRICE,
                                        amount = e.GasEvent.AMOUNT,
                                        fee = e.GasEvent.FEE,
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
                                        infused_value_raw = e.InfusionEvent.INFUSED_VALUE_RAW,
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
                                                name = e.OrganizationEvent.Organization.NAME
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
                                        value_raw = e.TokenEvent.VALUE_RAW,
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
                }
            );

            var queryString = select.ToQueryString();
                
            transactions = await select.ToArrayAsync();
            
            var responseTime = DateTime.Now - startTime;

            if ( responseTime.TotalSeconds > 1 )
            {
                Log.Warning($"Slow query: " + queryString);
            }

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
            { total_results = with_total == 1 ? totalResults : null, transactions = transactions };
    }
}
