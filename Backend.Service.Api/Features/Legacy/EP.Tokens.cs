using System;
using System.Linq;
using System.Net;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public partial class Endpoints
{
    [ProducesResponseType(typeof(TokenResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(TokenResult), "Returns the token on the backend.", false, 10)]
    public static TokenResult Tokens(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string symbol = "",
        string chain = "main",
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

            if ( !string.IsNullOrEmpty(symbol) ) query = query.Where(x => x.SYMBOL == symbol.ToUpper());

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
                mintable = x.MINTABLE,
                decimals = x.DECIMALS,
                current_supply = x.CURRENT_SUPPLY,
                current_supply_raw = x.CURRENT_SUPPLY_RAW,
                max_supply = x.MAX_SUPPLY,
                max_supply_raw = x.MAX_SUPPLY_RAW,
                burned_supply = x.BURNED_SUPPLY,
                burned_supply_raw = x.BURNED_SUPPLY_RAW,
                script_raw = x.SCRIPT_RAW,
                price = with_price == 1
                    ? new Price
                    {
                        usd = x.PRICE_USD != 0 ? x.PRICE_USD : null
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
                            hash = x.CreateEvent.Contract.HASH,
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
