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

public static class GetBlocks
{
    [ProducesResponseType(typeof(BlockResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(BlockResult), "Returns the block information from backend.", false, 10, cacheTag: "block")]
    public static async Task<BlockResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string id = "",
        string hash = "",
        string hash_partial = "",
        string height = "",
        string chain = "main",
        string date_less = "",
        string date_greater = "",
        int with_transactions = 0,
        int with_events = 0,
        int with_event_data = 0,
        int with_nft = 0,
        int with_fiat = 0,
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        Block[] blockArray;
        const string fiatCurrency = "USD";

        //chain is not considered a filter atm
        var filter = !string.IsNullOrEmpty(id) || !string.IsNullOrEmpty(hash) || !string.IsNullOrEmpty(hash_partial) ||
                     !string.IsNullOrEmpty(height) || !string.IsNullOrEmpty(date_less) ||
                     !string.IsNullOrEmpty(date_greater);

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

            if ( !string.IsNullOrEmpty(hash) && !ArgValidation.CheckHash(hash) )
                throw new ApiParameterException("Unsupported value for 'hash' parameter.");

            if ( !string.IsNullOrEmpty(hash_partial) && !ArgValidation.CheckHash(hash_partial) )
                throw new ApiParameterException("Unsupported value for 'hash_partial' parameter.");

            if ( !string.IsNullOrEmpty(height) && !ArgValidation.CheckNumber(height) )
                throw new ApiParameterException("Unsupported value for 'height' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if ( !string.IsNullOrEmpty(date_less) && !ArgValidation.CheckNumber(date_less) )
                throw new ApiParameterException("Unsupported value for 'date_less' parameter.");

            if ( !string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckNumber(date_greater) )
                throw new ApiParameterException("Unsupported value for 'date_greater' parameter.");

            #endregion

            var startTime = DateTime.Now;

            await using MainDbContext databaseContext = new();
            var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);

            //just need that since we build the model so it knows what we can use
            var query = databaseContext.Blocks.AsQueryable().AsNoTracking();

            #region Filtering

            if ( !string.IsNullOrEmpty(id) )
            {
                query = id.Length == 64 ? query.Where(x => x.HASH == id || x.HEIGHT == id) : query.Where(x => x.HEIGHT == id);
            }

            if ( !string.IsNullOrEmpty(hash) )
                query = query.Where(x => x.HASH == hash);

            if ( !string.IsNullOrEmpty(hash_partial) )
                query = query.Where(x => x.HASH.Contains(hash_partial));

            if ( !string.IsNullOrEmpty(height) )
                query = query.Where(x => x.HEIGHT == height);

            if ( !string.IsNullOrEmpty(date_less) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

            if ( !string.IsNullOrEmpty(date_greater) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Chain.NAME == chain);

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

            #region ResultArray

            if ( limit > 0 ) query = query.Skip(offset).Take(limit);

            blockArray = await query.Select(x => new Block
            {
                height = x.HEIGHT,
                hash = x.HASH,
                previous_hash = x.PREVIOUS_HASH,
                protocol = x.PROTOCOL,
                chain_address = x.ChainAddress.ADDRESS,
                validator_address = x.ValidatorAddress.ADDRESS,
                date = x.TIMESTAMP_UNIX_SECONDS.ToString(),
                reward = x.REWARD,
                transactions = with_transactions == 1 && x.Transactions != null
                    ? x.Transactions.Select(t => new Transaction
                    {
                        hash = t.HASH,
                        block_hash = x.HASH,
                        block_height = x.HEIGHT,
                        index = t.INDEX,
                        date = t.TIMESTAMP_UNIX_SECONDS.ToString(),
                        fee = t.FEE,
                        fee_raw = t.FEE_RAW,
                        script_raw = t.SCRIPT_RAW,
                        result = t.RESULT,
                        payload = t.PAYLOAD,
                        expiration = t.EXPIRATION.ToString(),
                        gas_price = t.GAS_PRICE,
                        gas_price_raw = t.GAS_PRICE_RAW,
                        gas_limit = t.GAS_LIMIT,
                        gas_limit_raw = t.GAS_LIMIT_RAW,
                        state = t.State.NAME,
                        sender = t.Sender != null
                            ? new Address
                            {
                                address_name = t.Sender.ADDRESS_NAME,
                                address = t.Sender.ADDRESS
                            }
                            : null,
                        gas_payer = t.GasPayer != null
                            ? new Address
                            {
                                address_name = t.GasPayer.ADDRESS_NAME,
                                address = t.GasPayer.ADDRESS
                            }
                            : null,
                        gas_target = t.GasTarget != null
                            ? new Address
                            {
                                address_name = t.GasTarget.ADDRESS_NAME,
                                address = t.GasTarget.ADDRESS
                            }
                            : null,
                        events = with_events == 1 && t.Events != null
                            ? t.Events.Select(e => new Event
                            {
                                event_id = e.ID,
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
                                    hash = e.Contract.HASH,
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
                                        mode_name = ( e.Nft.Series.SeriesMode != null
                                            ? e.Nft.Series.SeriesMode.MODE_NAME
                                            : null )!,
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
                                        address = e.TargetAddress != null
                                            ? new Address
                                            {
                                                address_name = e.TargetAddress.ADDRESS_NAME,
                                                address = e.TargetAddress.ADDRESS
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
                    }).ToArray()
                    : null
            }).ToArrayAsync();

            #endregion

            var responseTime = DateTime.Now - startTime;
            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Block()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }


        return new BlockResult {total_results = with_total == 1 ? totalResults : null, blocks = blockArray};
    }
}
