using System;
using System.Linq;
using System.Net;
using Database.Main;
using GhostDevs.Commons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GhostDevs.Service.Api;

public partial class Endpoints
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Token Price History on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-HistoryPriceResult'>HistoryPriceResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or name</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="symbol" example="SOUL"></param>
    /// <param name="date_less">Date (greater than), UTC unixseconds</param>
    /// <param name="date_greater">Date (greater than), UTC unixseconds</param>
    /// <param name="with_token" example="0">Return Data with <a href='#model-Token'>Token</a></param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [ProducesResponseType(typeof(HistoryPriceResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(HistoryPriceResult), "Returns the Token Price History on the backend.", false, 10)]
    public HistoryPriceResult HistoryPrices(
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
            using MainDbContext databaseContext = new();
            var query = databaseContext.TokenDailyPrices.AsQueryable().AsNoTracking();

            if ( !string.IsNullOrEmpty(symbol) )
                query = query.Where(x => x.Token.SYMBOL == symbol);

            //might work
            if ( !string.IsNullOrEmpty(date_less) )
                query = query.Where(x => x.DATE_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

            if ( !string.IsNullOrEmpty(date_greater) )
                query = query.Where(x => x.DATE_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));


            if ( with_total == 1 )
                totalResults = query.Count();

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

            historyArray = query.Select(x => new HistoryPrice
            {
                symbol = x.Token != null ? x.Token.SYMBOL : null,
                price = new Price
                {
                    usd = x.PRICE_USD != 0 ? x.PRICE_USD : null,
                    eur = x.PRICE_EUR != 0 ? x.PRICE_EUR : null,
                    gbp = x.PRICE_GBP != 0 ? x.PRICE_GBP : null,
                    jpy = x.PRICE_JPY != 0 ? x.PRICE_JPY : null,
                    cad = x.PRICE_CAD != 0 ? x.PRICE_CAD : null,
                    aud = x.PRICE_AUD != 0 ? x.PRICE_AUD : null,
                    cny = x.PRICE_CNY != 0 ? x.PRICE_CNY : null,
                    rub = x.PRICE_RUB != 0 ? x.PRICE_RUB : null
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
                        decimals = x.Token.DECIMALS,
                        current_supply = x.Token.CURRENT_SUPPLY,
                        max_supply = x.Token.MAX_SUPPLY,
                        burned_supply = x.Token.BURNED_SUPPLY,
                        script_raw = x.Token.SCRIPT_RAW
                    }
                    : null,
                date = x.DATE_UNIX_SECONDS.ToString()
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
            var logMessage = LogEx.Exception("TokenHistoryPrice()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new HistoryPriceResult
            {total_results = with_total == 1 ? totalResults : null, history_prices = historyArray};
    }
}
