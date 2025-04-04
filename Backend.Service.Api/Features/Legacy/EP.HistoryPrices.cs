using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetHistoryPrices
{
    [ProducesResponseType(typeof(HistoryPriceResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(HistoryPriceResult), "Returns the Token Price History on the backend.", false, 10)]
    public static async Task<HistoryPriceResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "date",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string symbol = "SOUL",
        string date_less = "",
        string date_greater = "",
        int with_token = 0,
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        HistoryPrice[] historyArray;
        var filter = !string.IsNullOrEmpty(symbol) || !string.IsNullOrEmpty(date_less) ||
                     !string.IsNullOrEmpty(date_greater);

        try
        {
            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, filter) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol) )
                throw new ApiParameterException("Unsupported value for 'address' parameter.");

            if ( !string.IsNullOrEmpty(date_less) && !ArgValidation.CheckNumber(date_less) )
                throw new ApiParameterException("Unsupported value for 'date_less' parameter.");

            if ( !string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckNumber(date_greater) )
                throw new ApiParameterException("Unsupported value for 'date_greater' parameter.");

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();
            var query = databaseContext.TokenDailyPrices.AsQueryable().AsNoTracking();

            if ( !string.IsNullOrEmpty(symbol) )
                query = query.Where(x => x.Token.SYMBOL == symbol);

            //might work
            if ( !string.IsNullOrEmpty(date_less) )
                query = query.Where(x => x.DATE_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

            if ( !string.IsNullOrEmpty(date_greater) )
                query = query.Where(x => x.DATE_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));


            if ( with_total == 1 )
                totalResults = await query.CountAsync();

            //in case we add more to sort
            if ( order_direction == "asc" )
                query = order_by switch
                {
                    "id" => query.OrderBy(x => x.ID),
                    "symbol" => query.OrderBy(x => x.Token.SYMBOL),
                    "date" => query.OrderBy(x => x.DATE_UNIX_SECONDS),
                    _ => query
                };
            else
                query = order_by switch
                {
                    "id" => query.OrderByDescending(x => x.ID),
                    "symbol" => query.OrderByDescending(x => x.Token.SYMBOL),
                    "date" => query.OrderByDescending(x => x.DATE_UNIX_SECONDS),
                    _ => query
                };

            if ( limit > 0 ) query = query.Skip(offset).Take(limit);

            historyArray = await query.Select(x => new HistoryPrice
            {
                symbol = x.Token != null ? x.Token.SYMBOL : null,
                price = new Price
                {
                    usd = x.PRICE_USD != 0 ? x.PRICE_USD : null
                },
                token = with_token == 1 && x.Token != null
                    ? new Token
                    {
                        symbol = x.Token.SYMBOL,
                        fungible = x.Token.FUNGIBLE,
                        transferable = x.Token.TRANSFERABLE,
                        finite = x.Token.FINITE,
                        divisible = x.Token.DIVISIBLE,
                        fiat = x.Token.FIAT,
                        fuel = x.Token.FUEL,
                        swappable = x.Token.SWAPPABLE,
                        burnable = x.Token.BURNABLE,
                        stakable = x.Token.STAKABLE,
                        mintable = x.Token.MINTABLE,
                        decimals = x.Token.DECIMALS,
                        current_supply = x.Token.CURRENT_SUPPLY,
                        current_supply_raw = x.Token.CURRENT_SUPPLY_RAW,
                        max_supply = x.Token.MAX_SUPPLY,
                        max_supply_raw = x.Token.MAX_SUPPLY_RAW,
                        burned_supply = x.Token.BURNED_SUPPLY,
                        burned_supply_raw = x.Token.BURNED_SUPPLY_RAW,
                        script_raw = x.Token.SCRIPT_RAW
                    }
                    : null,
                date = x.DATE_UNIX_SECONDS.ToString()
            }).ToArrayAsync();

            var responseTime = DateTime.Now - startTime;
            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("TokenHistoryPrice()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new HistoryPriceResult
            {total_results = with_total == 1 ? totalResults : null, history_prices = historyArray};
    }
}
