using System.Linq;
using Backend.Commons;

namespace Database.Main;

public static class TokenDailyPricesMethods
{
    // Checks if "TokenDailyPrices" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static void Upsert(MainDbContext databaseContext, long dateUnixSeconds, Token token,
        decimal usdPrice, bool saveChanges = true)
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

        entry.PRICE_USD = usdPrice;

        if ( saveChanges ) databaseContext.SaveChanges();
    }


    public static decimal Get(MainDbContext databaseContext, Token token, long dateUnixSeconds)
    {
        // Ensure it's a date without time
        dateUnixSeconds = UnixSeconds.GetDate(dateUnixSeconds);

        var entry = databaseContext.TokenDailyPrices.FirstOrDefault(x =>
            x.Token == token && x.DATE_UNIX_SECONDS == dateUnixSeconds);
        if ( entry == null ) return 0;

        return entry.PRICE_USD;
    }


    public static decimal Calculate(MainDbContext databaseContext, Chain chain, long dateUnixSeconds,
        string tokenSymbol, string priceInTokens)
    {
        return Get(databaseContext, chain, dateUnixSeconds, tokenSymbol) *
               TokenMethods.ToDecimal(priceInTokens, tokenSymbol);
    }


    public static decimal Get(MainDbContext databaseContext, Chain chain, long dateUnixSeconds, string symbol)
    {
        return Get(databaseContext, TokenMethods.Get(databaseContext, chain, symbol), dateUnixSeconds);
    }
}
