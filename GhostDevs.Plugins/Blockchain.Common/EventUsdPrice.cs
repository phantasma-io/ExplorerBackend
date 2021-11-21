using Database.Main;
using GhostDevs.PluginEngine;
using Serilog;
using System;
using System.Linq;

namespace GhostDevs.Blockchain
{
    public partial class BlockchainCommonPlugin : Plugin, IDBAccessPlugin
    {
        // Max events to be processed during one call.
        private static readonly int maxEventUsdPricesProcessedPerSession = 1000;

        private int EventUsdPricesFillForArray(MainDbContext databaseContext, Event[] events, TokenMethods.TokenPrice[] todaysTokenPrices)
        {
            var pricesProcessed = 0;

            for (var i = 0; i < events.Count(); i++)
            {
                try
                {
                    events[i].PRICE_USD = TokenDailyPricesMethods.Calculate(databaseContext, events[i].ChainId, events[i].DATE_UNIX_SECONDS, events[i].QuoteSymbol.SYMBOL, events[i].PRICE, "USD");

                    if (events[i].PRICE_USD == 0)
                    {
                        events[i].PRICE_USD = (decimal)TokenMethods.CalculatePrice(todaysTokenPrices, events[i].PRICE, events[i].QuoteSymbol.SYMBOL);
                    }
                }
                catch
                {
                    Log.Warning($"Event USD price can't be calculated using token price '{events[i].PRICE}'");
                }

                pricesProcessed++;
            }

            return pricesProcessed;
        }

        public void EventUsdPricesFill()
        {
            DateTime startTime = DateTime.Now;

            var pricesProcessed = 0;

            using (var databaseContext = new MainDbContext())
            {
                var todaysTokenPrices = TokenMethods.GetPrices(databaseContext, "USD");

                // Fill events with uninitialized USD price.
                // Here we do not refresh older prices, just fill empty ones.
                var events = databaseContext.Events
                    .Where(x => x.PRICE != null && x.PRICE != "0" && x.PRICE_USD == 0).OrderBy(x => x.TIMESTAMP_UNIX_SECONDS).Take(maxEventUsdPricesProcessedPerSession).ToArray();

                pricesProcessed += EventUsdPricesFillForArray(databaseContext, events, todaysTokenPrices);

                if (pricesProcessed > 0)
                    databaseContext.SaveChanges();
            }

            TimeSpan processTime = DateTime.Now - startTime;
            Log.Information($"{Name} plugin: Processed events USD prices: {pricesProcessed} processed in {Math.Round(processTime.TotalSeconds, 3)} sec.");
        }
    }
}
