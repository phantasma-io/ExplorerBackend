using System.Collections.Generic;
using System.Linq;
using GhostDevs.Commons;

namespace Database.Main;

public static class TokenDailyPricesMethods
{
    // Checks if "TokenDailyPrices" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static void Upsert(MainDbContext databaseContext, long dateUnixSeconds, Token token,
        Dictionary<string, decimal> pricePairs, bool saveChanges = true)
    {
        if ( token == null ) return;

        dateUnixSeconds = UnixSeconds.GetDate(dateUnixSeconds);

        var entry = databaseContext.TokenDailyPrices.FirstOrDefault(x =>
            x.Token == token && x.DATE_UNIX_SECONDS == dateUnixSeconds);
        if ( entry == null )
        {
            entry = new TokenDailyPrice {DATE_UNIX_SECONDS = dateUnixSeconds, Token = token};
            databaseContext.TokenDailyPrices.Add(entry);
        }

        TokenPriceStateMethods.Upsert(databaseContext, token, true);

        foreach ( var (key, value) in pricePairs )
            switch ( key.ToUpper() )
            {
                case "SOUL":
                    entry.PRICE_SOUL = value;
                    break;
                case "NEO":
                    entry.PRICE_NEO = value;
                    break;
                case "ETH":
                    entry.PRICE_ETH = value;
                    break;
                case "AUD":
                    entry.PRICE_AUD = value;
                    break;
                case "CAD":
                    entry.PRICE_CAD = value;
                    break;
                case "CNY":
                    entry.PRICE_CNY = value;
                    break;
                case "EUR":
                    entry.PRICE_EUR = value;
                    break;
                case "GBP":
                    entry.PRICE_GBP = value;
                    break;
                case "JPY":
                    entry.PRICE_JPY = value;
                    break;
                case "RUB":
                    entry.PRICE_RUB = value;
                    break;
                case "USD":
                    entry.PRICE_USD = value;
                    break;
            }

        if ( saveChanges ) databaseContext.SaveChanges();
    }


    public static decimal Get(MainDbContext databaseContext, Token token, long dateUnixSeconds, string priceSymbol)
    {
        // Ensure it's a date without time
        dateUnixSeconds = UnixSeconds.GetDate(dateUnixSeconds);

        var entry = databaseContext.TokenDailyPrices.FirstOrDefault(x =>
            x.Token == token && x.DATE_UNIX_SECONDS == dateUnixSeconds);
        if ( entry == null ) return 0;

        return priceSymbol.ToUpper() switch
        {
            "SOUL" => entry.PRICE_SOUL,
            "NEO" => entry.PRICE_NEO,
            "ETH" => entry.PRICE_ETH,
            "AUD" => entry.PRICE_AUD,
            "CAD" => entry.PRICE_CAD,
            "CNY" => entry.PRICE_CNY,
            "EUR" => entry.PRICE_EUR,
            "GBP" => entry.PRICE_GBP,
            "JPY" => entry.PRICE_JPY,
            "RUB" => entry.PRICE_RUB,
            "USD" => entry.PRICE_USD,
            _ => 0
        };
    }


    public static decimal Calculate(MainDbContext databaseContext, Chain chain, long dateUnixSeconds,
        string tokenSymbol, string priceInTokens, string outPriceSymbol)
    {
        return Get(databaseContext, chain, dateUnixSeconds, tokenSymbol, outPriceSymbol) *
               TokenMethods.ToDecimal(priceInTokens, tokenSymbol);
    }


    public static decimal CalculateFromUsd(Dictionary<long, TokenDailyPrice> soulDailyTokenPrices,
        Dictionary<long, TokenDailyPrice> outTokenDailyPrices, long dateUnixSeconds, decimal priceInUsd,
        string outPriceSymbol)
    {
        if ( outPriceSymbol.ToUpper() == "USD" ) return priceInUsd; // No calculation is needed.

        var dailyPrices =
            soulDailyTokenPrices.Where(x => x.Key == dateUnixSeconds);

        var keyValuePairs = dailyPrices.ToList();
        if ( !keyValuePairs.Any() )
        {
            dailyPrices = soulDailyTokenPrices.Where(x => x.Key == UnixSeconds.AddDays(dateUnixSeconds, -1));
            if ( !dailyPrices.Any() )
                // There's a problem with prices, just return 0
                return 0;
        }

        var soulTokenDailyPrice = keyValuePairs.Select(x => x.Value).FirstOrDefault();

        if ( TokenMethods.GetSupportedFiatSymbols().Contains(outPriceSymbol) )
            // We convert USD to another Fiat.
            // We are doing it through SOUL.
            // Converting price to SOUL:
            if ( soulTokenDailyPrice != null )
            {
                var soulUsdPrice = soulTokenDailyPrice.PRICE_USD;
                if ( soulUsdPrice == 0 ) return 0;

                var priceInSoul = priceInUsd / soulUsdPrice;
                return priceInSoul * outPriceSymbol.ToUpper() switch
                {
                    "SOUL" => soulTokenDailyPrice.PRICE_SOUL,
                    "NEO" => soulTokenDailyPrice.PRICE_NEO,
                    "AUD" => soulTokenDailyPrice.PRICE_AUD,
                    "CAD" => soulTokenDailyPrice.PRICE_CAD,
                    "CNY" => soulTokenDailyPrice.PRICE_CNY,
                    "EUR" => soulTokenDailyPrice.PRICE_EUR,
                    "GBP" => soulTokenDailyPrice.PRICE_GBP,
                    "JPY" => soulTokenDailyPrice.PRICE_JPY,
                    "RUB" => soulTokenDailyPrice.PRICE_RUB,
                    "USD" => soulTokenDailyPrice.PRICE_USD,
                    _ => 0
                };
            }

        // We convert USD price to token price.
        var outTokenDailyPrice = outTokenDailyPrices?.Where(x => x.Key == dateUnixSeconds)
            .Select(x => x.Value)
            .FirstOrDefault();

        var outTokenPriceInUsd = outTokenDailyPrice?.PRICE_USD ?? 0;
        if ( outTokenPriceInUsd == 0 ) return 0;

        return priceInUsd / outTokenPriceInUsd;
    }


    public static decimal Get(MainDbContext databaseContext, Chain chain, long dateUnixSeconds, string symbol,
        string priceSymbol)
    {
        return Get(databaseContext, TokenMethods.Get(databaseContext, chain, symbol), dateUnixSeconds, priceSymbol);
    }
}
