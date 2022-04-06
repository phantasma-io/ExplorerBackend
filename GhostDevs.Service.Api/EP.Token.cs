using System;
using System.Linq;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(TokenResult), "Returns the token on the backend.", false, 10)]
    public TokenResult Tokens(
        [APIParameter("Order by [id, symbol]", "string")]
        string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("symbol", "string")] string symbol = "",
        [APIParameter("Chain name (ex. 'main')", "string")]
        string chain = "",
        [APIParameter("with prices", "integer")]
        int with_price = 0,
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0)
    {
        long totalResults = 0;
        Token[] tokenArray;

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

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new APIException("Unsupported value for 'chain' parameter.");

            var startTime = DateTime.Now;

            var query = _context.Tokens.AsQueryable();

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
            var logMessage = LogEx.Exception("Token()", exception);

            throw new APIException(logMessage, exception);
        }

        return new TokenResult {total_results = with_total == 1 ? totalResults : null, tokens = tokenArray};
    }
}
