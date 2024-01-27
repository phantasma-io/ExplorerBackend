using System.Linq;
using System.Threading.Tasks;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;

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


    private static async Task<decimal> GetAsync(MainDbContext databaseContext, Token token, long dateUnixSeconds)
    {
        // Ensure it's a date without time
        dateUnixSeconds = UnixSeconds.GetDate(dateUnixSeconds);

        var entry = await databaseContext.TokenDailyPrices.FirstOrDefaultAsync(x =>
            x.Token == token && x.DATE_UNIX_SECONDS == dateUnixSeconds);
        if ( entry == null ) return 0;

        return entry.PRICE_USD;
    }


    public static async Task<decimal> CalculateAsync(MainDbContext databaseContext, Chain chain, long dateUnixSeconds,
        string tokenSymbol, string priceInTokens)
    {
        return await GetAsync(databaseContext, chain, dateUnixSeconds, tokenSymbol) *
               TokenMethods.ToDecimal(priceInTokens, tokenSymbol);
    }


    private static async Task<decimal> GetAsync(MainDbContext databaseContext, Chain chain, long dateUnixSeconds, string symbol)
    {
        return await GetAsync(databaseContext, await TokenMethods.GetAsync(databaseContext, chain, symbol), dateUnixSeconds);
    }
}
