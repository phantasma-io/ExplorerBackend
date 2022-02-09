using System;
using System.Collections.Generic;
using System.Linq;
using Database.Main;
using GhostDevs.PluginEngine;
using Serilog;

namespace GhostDevs.Blockchain;

public partial class BlockchainCommonPlugin : Plugin, IDBAccessPlugin
{
    // Max events to be processed during one call.
    private const int MaxEventUsdPricesProcessedPerSession = 1000;


    private void EventUsdPricesFill()
    {
        var startTime = DateTime.Now;

        var pricesProcessed = 0;

        using ( var databaseContext = new MainDbContext() )
        {
            var tokenPrices = TokenMethods.GetPrices(databaseContext, "USD");

            // Fill events with uninitialized USD price.
            // Here we do not refresh older prices, just fill empty ones.

            var eventTimeStart = DateTime.Now;

            //never processed
            var eventsFirst = databaseContext.MarketEvents.Where(x =>
                    !databaseContext.MarketEventFiatPrices.Any(y => y.MarketEventId == x.ID))
                .OrderBy(x => x.Event.TIMESTAMP_UNIX_SECONDS).Take(MaxEventUsdPricesProcessedPerSession).ToList();

            //now get the events we never had
            //at least processed once
            var leftToTake = MaxEventUsdPricesProcessedPerSession - eventsFirst.Count;
            var eventsNoPrice = new List<MarketEvent>();
            if ( leftToTake > 0 )
                eventsNoPrice = databaseContext.MarketEvents.Where(x =>
                        x.MarketEventFiatPrice.PRICE_USD == 0 || x.MarketEventFiatPrice.PRICE_END_USD == 0)
                    .OrderBy(x => x.Event.TIMESTAMP_UNIX_SECONDS).Take(leftToTake).ToList();

            var events = eventsFirst.Concat(eventsNoPrice).ToList();

            var eventTimeEnd = DateTime.Now - eventTimeStart;
            Log.Verbose(
                "Got {Count} Events loaded, First Timers {First}, Rechecking {Recheck}, processed in {Time} sec",
                events.Count, eventsFirst.Count, eventsNoPrice.Count, Math.Round(eventTimeEnd.TotalSeconds, 3));

            const string fiat = "USD";

            foreach ( var marketEvent in events )
                try
                {
                    MarketEventFiatPriceMethods.Upsert(databaseContext, marketEvent,
                        GetSymbolPrice(databaseContext, marketEvent, fiat, tokenPrices, false),
                        GetSymbolPrice(databaseContext, marketEvent, fiat, tokenPrices, true), fiat);

                    pricesProcessed++;
                }
                catch
                {
                    Log.Warning("Event USD price can't be calculated using token price '{Price}'", marketEvent.PRICE);
                }

            if ( pricesProcessed > 0 ) databaseContext.SaveChanges();
        }

        var processTime = DateTime.Now - startTime;
        Log.Information(
            "{Name} plugin: Processed events USD prices: {PricesProcessed} processed in {ProcessTime} sec", Name,
            pricesProcessed, Math.Round(processTime.TotalSeconds, 3));
    }


    private static decimal GetSymbolPrice(MainDbContext databaseContext, MarketEvent marketEvent, string fiat,
        IEnumerable<TokenMethods.TokenPrice> tokenPrices, bool endPrice)
    {
        var toCalculate = endPrice ? marketEvent.END_PRICE : marketEvent.PRICE;

        var price = TokenDailyPricesMethods.Calculate(databaseContext, marketEvent.Event.ChainId,
            marketEvent.Event.DATE_UNIX_SECONDS, marketEvent.QuoteToken.SYMBOL, toCalculate, fiat);

        if ( price == 0 )
            price = ( decimal ) TokenMethods.CalculatePrice(tokenPrices, toCalculate, marketEvent.QuoteToken.SYMBOL);

        return price;
    }
}
