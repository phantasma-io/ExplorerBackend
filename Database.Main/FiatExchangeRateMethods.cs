using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class FiatExchangeRateMethods
{
    // Checks if "FiatExchangeRate" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static void Upsert(MainDbContext databaseContext, string symbol, decimal usdPrice, bool saveChanges = true)
    {
        var entry = databaseContext.FiatExchangeRates.FirstOrDefault(x => x.SYMBOL == symbol);
        if ( entry != null )
            entry.USD_PRICE = usdPrice;
        else
        {
            entry = new FiatExchangeRate {SYMBOL = symbol, USD_PRICE = usdPrice};
            databaseContext.FiatExchangeRates.Add(entry);
        }

        if ( saveChanges ) databaseContext.SaveChanges();
    }

    // Gets fiat prices dictionary against USD.
    // Dictionary key contains fiat currency symbol, and value contains price in USD.
    public static Dictionary<string, decimal> GetPrices(MainDbContext databaseContext)
    {
        return databaseContext.FiatExchangeRates.Select(x => new {key = x.SYMBOL, value = x.USD_PRICE})
            .ToDictionary(x => x.key, x => x.value);
    }


    public static decimal Convert(Dictionary<string, decimal> fiatPricesInUsd, decimal price, string fromSymbol,
        string toSymbol)
    {
        if ( string.IsNullOrEmpty(fromSymbol) || string.IsNullOrEmpty(toSymbol) || fromSymbol == toSymbol )
            return price; // No calculation is needed.

        var usdPrice = fiatPricesInUsd.Where(x => x.Key == fromSymbol).Select(x => x.Value).SingleOrDefault();
        if ( usdPrice == 0 ) return 0;

        price /= usdPrice;

        var toSymbolPrice = fiatPricesInUsd.Where(x => x.Key == toSymbol).Select(x => x.Value).SingleOrDefault();
        return price * toSymbolPrice;
    }
}
