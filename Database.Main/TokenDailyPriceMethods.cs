using System.Collections.Generic;
using System.Linq;
using GhostDevs.Commons;

namespace Database.Main;

public static class TokenDailyPricesMethods
{
    // Checks if "TokenDailyPrices" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static void Upsert(MainDbContext databaseContext, long dateUnixSeconds, int chainId, string symbol,
        Dictionary<string, decimal> pricePairs, bool saveChanges = true)
    {
        var token = TokenMethods.Get(databaseContext, chainId, symbol);
        if ( token == null ) return;

        var entry = databaseContext.TokenDailyPrices
            .Where(x => x.Token == token && x.DATE_UNIX_SECONDS == dateUnixSeconds).FirstOrDefault();
        if ( entry == null )
        {
            entry = new TokenDailyPrice {DATE_UNIX_SECONDS = dateUnixSeconds, Token = token};
            databaseContext.TokenDailyPrices.Add(entry);
        }

        foreach ( var pricePair in pricePairs )
            switch ( pricePair.Key.ToUpper() )
            {
                case "SOUL":
                    entry.PRICE_SOUL = pricePair.Value;
                    break;
                case "NEO":
                    entry.PRICE_NEO = pricePair.Value;
                    break;
                case "ETH":
                    entry.PRICE_ETH = pricePair.Value;
                    break;
                case "AUD":
                    entry.PRICE_AUD = pricePair.Value;
                    break;
                case "CAD":
                    entry.PRICE_CAD = pricePair.Value;
                    break;
                case "CNY":
                    entry.PRICE_CNY = pricePair.Value;
                    break;
                case "EUR":
                    entry.PRICE_EUR = pricePair.Value;
                    break;
                case "GBP":
                    entry.PRICE_GBP = pricePair.Value;
                    break;
                case "JPY":
                    entry.PRICE_JPY = pricePair.Value;
                    break;
                case "RUB":
                    entry.PRICE_RUB = pricePair.Value;
                    break;
                case "USD":
                    entry.PRICE_USD = pricePair.Value;
                    break;
            }

        if ( saveChanges ) databaseContext.SaveChanges();
    }


    public static decimal Get(MainDbContext databaseContext, int chainId, long dateUnixSeconds, string symbol,
        string priceSymbol)
    {
        var token = TokenMethods.Get(databaseContext, chainId, symbol);
        if ( token == null ) return 0;

        // Ensure it's a date without time
        dateUnixSeconds = UnixSeconds.GetDate(dateUnixSeconds);

        var entry = databaseContext.TokenDailyPrices
            .Where(x => x.Token == token && x.DATE_UNIX_SECONDS == dateUnixSeconds).FirstOrDefault();
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


    public static decimal Calculate(MainDbContext databaseContext, int chainId, long dateUnixSeconds,
        string tokenSymbol, string PriceInTokens, string outPriceSymbol)
    {
        return Get(databaseContext, chainId, dateUnixSeconds, tokenSymbol, outPriceSymbol) *
               TokenMethods.ToDecimal(PriceInTokens, tokenSymbol);
    }


    public static decimal Calculate(MainDbContext databaseContext, int chainId, long dateUnixSeconds,
        string tokenSymbol, decimal PriceInTokens, string outPriceSymbol)
    {
        return Get(databaseContext, chainId, dateUnixSeconds, tokenSymbol, outPriceSymbol) * PriceInTokens;
    }


    public static decimal CalculateFromUsd(Dictionary<long, TokenDailyPrice> soulDailyTokenPrices,
        Dictionary<long, TokenDailyPrice> outTokenDailyPrices, long dateUnixSeconds, decimal priceInUsd,
        string outPriceSymbol)
    {
        if ( outPriceSymbol.ToUpper() == "USD" ) return priceInUsd; // No calculation is needed.

        var dailyPrices =
            soulDailyTokenPrices.Where(x => x.Key == dateUnixSeconds);

        if ( dailyPrices.Count() == 0 )
        {
            dailyPrices = soulDailyTokenPrices.Where(x => x.Key == UnixSeconds.AddDays(dateUnixSeconds, -1));
            if ( dailyPrices.Count() == 0 )
                // There's a problem with prices, just return 0
                return 0;
        }

        var soulTokenDailyPrice = dailyPrices.Select(x => x.Value).FirstOrDefault();

        if ( TokenMethods.GetSupportedFiatSymbols().Contains(outPriceSymbol) )
        {
            // We convert USD to another Fiat.
            // We are doing it through SOUL.
            // Converting price to SOUL:
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

        var outTokenPriceInUsd = outTokenDailyPrice == null ? 0 : outTokenDailyPrice.PRICE_USD;
        if ( outTokenPriceInUsd == 0 ) return 0;

        return priceInUsd / outTokenPriceInUsd;
    }
}
