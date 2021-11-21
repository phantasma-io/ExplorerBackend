using System.Collections.Generic;
using System.Linq;

namespace Database.Main
{
    public static class FiatExchangeRateMethods
    {
        // Checks if "FiatExchangeRate" table has entry with given name,
        // and adds new entry, if there's no entry available.
        // Returns new or existing entry's Id.
        public static void Upsert(MainDbContext databaseContext, string symbol, decimal usdPrice, bool saveChanges = true)
        {
            var entry = databaseContext.FiatExchangeRates.Where(x => x.SYMBOL.ToUpper() == symbol.ToUpper()).FirstOrDefault();
            if (entry != null)
            {
                entry.USD_PRICE = usdPrice;
            }
            else
            {
                entry = new FiatExchangeRate { SYMBOL = symbol, USD_PRICE = usdPrice };
                databaseContext.FiatExchangeRates.Add(entry);
            }

            if(saveChanges)
                databaseContext.SaveChanges();
        }
        public static decimal Convert(MainDbContext databaseContext, decimal price, string fromSymbol, string toSymbol)
        {
            var usdPrice = databaseContext.FiatExchangeRates.Where(x => x.SYMBOL == fromSymbol).Select(x => x.USD_PRICE).SingleOrDefault();
            if (usdPrice == 0)
                return 0;

            price /= usdPrice;

            var toSymbolPrice = databaseContext.FiatExchangeRates.Where(x => x.SYMBOL == toSymbol).Select(x => x.USD_PRICE).SingleOrDefault();
            return price * toSymbolPrice;
        }
        // Gets fiat prices dictionary against USD.
        // Dictionary key contains fiat currency symbol, and value contains price in USD.
        public static Dictionary<string, decimal> GetPrices(MainDbContext efDatabaseContext)
        {
            return efDatabaseContext.FiatExchangeRates.Select(x => new { key = x.SYMBOL, value = x.USD_PRICE }).ToDictionary(x => x.key, x => x.value);
        }
        public static decimal Convert(Dictionary<string, decimal> fiatPricesInUsd, decimal price, string fromSymbol, string toSymbol)
        {
            if (string.IsNullOrEmpty(fromSymbol) || string.IsNullOrEmpty(toSymbol) || fromSymbol.ToUpper() == toSymbol.ToUpper())
                return price; // No calculation is needed.

            var usdPrice = fiatPricesInUsd.Where(x => x.Key == fromSymbol).Select(x => x.Value).SingleOrDefault();
            if (usdPrice == 0)
                return 0;

            price /= usdPrice;

            var toSymbolPrice = fiatPricesInUsd.Where(x => x.Key == toSymbol).Select(x => x.Value).SingleOrDefault();
            return price * toSymbolPrice;
        }
        public static decimal ConvertEx(TokenMethods.TokenPrice[] tokenPrices, Dictionary<string, decimal> fiatPricesInUsd, string priceInTokens, string quoteSymbol, decimal price, string fromSymbol, string toSymbol)
        {
            if (price == 0)
            {
                // This is needed to calculate today's prices when usd price is not yet available.
                // TODO think of something better.
                if (!string.IsNullOrEmpty(priceInTokens) && !string.IsNullOrEmpty(quoteSymbol))
                    return (decimal)TokenMethods.CalculatePrice(tokenPrices, priceInTokens, quoteSymbol);
                else
                    return 0;
            }

            if (string.IsNullOrEmpty(fromSymbol) || string.IsNullOrEmpty(toSymbol) || fromSymbol.ToUpper() == toSymbol.ToUpper())
                return price; // No calculation is needed.

            var usdPrice = fiatPricesInUsd.Where(x => x.Key == fromSymbol).Select(x => x.Value).SingleOrDefault();
            if (usdPrice == 0)
                return 0;

            price /= usdPrice;

            var toSymbolPrice = fiatPricesInUsd.Where(x => x.Key == toSymbol).Select(x => x.Value).SingleOrDefault();

            return price * toSymbolPrice;
        }
    }
}