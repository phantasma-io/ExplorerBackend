using System;
using System.Linq;
using System.Text.RegularExpressions;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
using Token = GhostDevs.Service.ApiResults.Token;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(HistoryPriceResult), "Returns the token on the backend.", false, 10)]
    public HistoryPriceResult HistoryPrices(
        [APIParameter("Order by [id, symbol, date]", "string")]
        string order_by = "date",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("symbol", "string")] string symbol = "SOUL",
        [APIParameter("with token info", "integer")]
        int with_token = 0,
        [APIParameter("Date day match (matches whole given day)", "string")]
        string date_day = "",
        [APIParameter("Date (less than)", "string")]
        string date_less = "",
        [APIParameter("Date (greater than)", "string")]
        string date_greater = "")
    {
        HistoryPrice[] historyArray;

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

                if ( !string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol) )
                    throw new APIException("Unsupported value for 'address' parameter.");

                if ( !string.IsNullOrEmpty(date_day) && !Regex.IsMatch(date_day, @"^[0-9.]+$") )
                    throw new APIException("Unsupported value for 'date_day' parameter.");

                if ( !string.IsNullOrEmpty(date_less) && !Regex.IsMatch(date_less, @"^[0-9.]+$") )
                    throw new APIException("Unsupported value for 'date_less' parameter.");

                if ( !string.IsNullOrEmpty(date_greater) && !Regex.IsMatch(date_greater, @"^[0-9.]+$") )
                    throw new APIException("Unsupported value for 'date_greater' parameter.");

                var startTime = DateTime.Now;

                var query = databaseContext.TokenDailyPrices.AsQueryable();

                if ( !string.IsNullOrEmpty(symbol) )
                    query = query.Where(x => string.Equals(x.Token.SYMBOL.ToUpper(), symbol.ToUpper()));

                //TODO fix date stuff
                if ( !string.IsNullOrEmpty(date_day) )
                    query = query.Where(x => x.DATE_UNIX_SECONDS == UnixSeconds.FromDateTimeString(date_day));

                if ( !string.IsNullOrEmpty(date_greater) )
                    query = query.Where(x => x.DATE_UNIX_SECONDS >= UnixSeconds.FromDateTimeString(date_greater));

                if ( !string.IsNullOrEmpty(date_less) )
                    query = query.Where(x => x.DATE_UNIX_SECONDS <= UnixSeconds.FromDateTimeString(date_less));

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

                var queryResults = query.Skip(offset).Take(limit).ToList();

                historyArray = queryResults.Select(x => new HistoryPrice
                {
                    symbol = x.Token?.SYMBOL,
                    price = new Price
                    {
                        date = x.DATE_UNIX_SECONDS.ToString(),
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
                        : null
                }).ToArray();

                var responseTime = DateTime.Now - startTime;
                Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
            }
            catch ( APIException )
            {
                throw;
            }
            catch ( Exception exception )
            {
                var logMessage = LogEx.Exception("Address()", exception);

                throw new APIException(logMessage, exception);
            }
        }

        return new HistoryPriceResult {history_prices = historyArray};
    }
}
