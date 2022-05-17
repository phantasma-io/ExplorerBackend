using System;
using System.Linq;
using System.Net;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Contract = GhostDevs.Service.Api.Contract;
using Event = GhostDevs.Service.Api.Event;
using StringEvent = GhostDevs.Service.Api.StringEvent;
using Token = GhostDevs.Service.Api.Token;
using TokenLogo = GhostDevs.Service.Api.TokenLogo;

namespace GhostDevs.Service;

public partial class Endpoints
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the token on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-TokenResult'>TokenResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or symbol</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="symbol" example="SOUL"></param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="with_price" example="0">Return data with <a href='#model-Price'>Prices</a> </param>
    /// <param name="with_creation_event" example="0">Return data with <a href='#model-Event'>Event</a> of the creation</param>
    /// <param name="with_logo" example="0">Return data with <a href='#model-TokenLogo'>Logo</a> Information</param>
    /// <param name="with_total" example="0">Returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [ProducesResponseType(typeof(TokenResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(TokenResult), "Returns the token on the backend.", false, 10)]
    public TokenResult Tokens(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string symbol = "",
        string chain = "",
        int with_price = 0,
        int with_creation_event = 0,
        int with_logo = 0,
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        Token[] tokenArray;

        try
        {
            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol) )
                throw new ApiParameterException("Unsupported value for 'address' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            var startTime = DateTime.Now;
            using MainDbContext databaseContext = new();
            var query = databaseContext.Tokens.AsQueryable().AsNoTracking();

            if ( !string.IsNullOrEmpty(symbol) ) query = query.Where(x => x.SYMBOL == symbol);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Chain.NAME == chain);

            // Count total number of results before adding order and limit parts of query.
            if ( with_total == 1 )
                totalResults = query.Count();

            //in case we add more to sort
            if ( order_direction == "asc" )
                query = order_by switch
                {
                    "id" => query.OrderBy(x => x.ID),
                    "symbol" => query.OrderBy(x => x.SYMBOL),
                    _ => query
                };
            else
                query = order_by switch
                {
                    "id" => query.OrderByDescending(x => x.ID),
                    "symbol" => query.OrderByDescending(x => x.SYMBOL),
                    _ => query
                };

            tokenArray = query.Skip(offset).Take(limit).Select(x => new Token
            {
                symbol = x.SYMBOL,
                fungible = x.FUNGIBLE,
                transferable = x.TRANSFERABLE,
                finite = x.FINITE,
                divisible = x.DIVISIBLE,
                fiat = x.FIAT,
                fuel = x.FUEL,
                swappable = x.SWAPPABLE,
                burnable = x.BURNABLE,
                stakable = x.STAKABLE,
                decimals = x.DECIMALS,
                current_supply = x.CURRENT_SUPPLY,
                max_supply = x.MAX_SUPPLY,
                burned_supply = x.BURNED_SUPPLY,
                script_raw = x.SCRIPT_RAW,
                price = with_price == 1
                    ? new Price
                    {
                        usd = x.PRICE_USD != 0 ? x.PRICE_USD : null,
                        eur = x.PRICE_EUR != 0 ? x.PRICE_EUR : null,
                        gbp = x.PRICE_GBP != 0 ? x.PRICE_GBP : null,
                        jpy = x.PRICE_JPY != 0 ? x.PRICE_JPY : null,
                        cad = x.PRICE_CAD != 0 ? x.PRICE_CAD : null,
                        aud = x.PRICE_AUD != 0 ? x.PRICE_AUD : null,
                        cny = x.PRICE_CNY != 0 ? x.PRICE_CNY : null,
                        rub = x.PRICE_RUB != 0 ? x.PRICE_RUB : null
                    }
                    : null,
                create_event = with_creation_event == 1 && x.CreateEvent != null
                    ? new Event
                    {
                        event_id = x.ID,
                        chain = x.CreateEvent.Chain.NAME.ToLower(),
                        date = x.CreateEvent.TIMESTAMP_UNIX_SECONDS.ToString(),
                        block_hash = x.CreateEvent.Transaction.Block.HASH,
                        transaction_hash = x.CreateEvent.Transaction.HASH,
                        token_id = x.CreateEvent.TOKEN_ID,
                        event_kind = x.CreateEvent.EventKind.NAME,
                        address = x.CreateEvent.Address.ADDRESS,
                        address_name = x.CreateEvent.Address.ADDRESS_NAME,
                        contract = new Contract
                        {
                            name = x.CreateEvent.Contract.NAME,
                            hash = ContractMethods.Prepend0x(x.CreateEvent.Contract.HASH, x.CreateEvent.Chain.NAME),
                            symbol = x.CreateEvent.Contract.SYMBOL
                        },
                        string_event = x.CreateEvent.StringEvent != null
                            ? new StringEvent
                            {
                                string_value = x.CreateEvent.StringEvent.STRING_VALUE
                            }
                            : null
                    }
                    : null,
                token_logos = with_logo == 1 && x.TokenLogos != null
                    ? x.TokenLogos.Select(t => new TokenLogo
                    {
                        type = t.TokenLogoType.NAME,
                        url = t.URL
                    }).ToArray()
                    : null
            }).ToArray();
            var responseTime = DateTime.Now - startTime;
            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Token()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new TokenResult {total_results = with_total == 1 ? totalResults : null, tokens = tokenArray};
    }
}
