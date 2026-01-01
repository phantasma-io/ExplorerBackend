using System;
using System.Collections.Generic;
using System.Linq;
using Backend.PluginEngine;
using Database.Main;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Blockchain;

public partial class BlockchainCommonPlugin : Plugin, IDBAccessPlugin
{
    // Max events to be processed during one call.
    private const int MaxEventUsdPricesProcessedPerSession = 1000;
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class MarketEventPayload
    {
        [JsonPropertyName("market_event")]
        public MarketEventData MarketEvent { get; set; }
    }

    private sealed class MarketEventData
    {
        [JsonPropertyName("quote_token")]
        public string QuoteToken { get; set; }

        [JsonPropertyName("price")]
        public string Price { get; set; }

        [JsonPropertyName("end_price")]
        public string EndPrice { get; set; }
    }


    private void EventUsdPricesFill()
    {
        var startTime = DateTime.Now;

        var pricesProcessed = 0;

        using (var databaseContext = new MainDbContext())
        {
            var tokenPrices = TokenMethods.GetPrices(databaseContext, "USD");

            var marketKinds = new[]
            {
                "OrderCancelled",
                "OrderClosed",
                "OrderCreated",
                "OrderFilled",
                "OrderBid"
            };

            var loadStart = DateTime.Now;

            var events = databaseContext.Events
                .Where(e => marketKinds.Contains(e.EventKind.NAME))
                .Where(e => e.MarketEvent != null &&
                            (e.MarketEvent.MarketEventFiatPrice == null ||
                              e.MarketEvent.MarketEventFiatPrice.PRICE_USD == 0 ||
                              e.MarketEvent.MarketEventFiatPrice.PRICE_END_USD == 0))
                .OrderBy(e => e.ID)
                .Take(MaxEventUsdPricesProcessedPerSession)
                .Select(e => new
                {
                    Event = e,
                    Chain = e.Chain
                })
                .ToList();

            var eventIds = events.Select(x => x.Event.ID).ToList();
            var marketEventsByEventId = databaseContext.MarketEvents
                .Where(x => eventIds.Contains(x.EventId))
                .ToDictionary(x => x.EventId, x => x);

            var eventTimeEnd = DateTime.Now - loadStart;
            Log.Verbose(
                "Got {Count} market events for pricing in {Time} sec",
                events.Count, Math.Round(eventTimeEnd.TotalSeconds, 3));

            foreach (var evt in events)
            {
                if (!marketEventsByEventId.TryGetValue(evt.Event.ID, out var marketEvent))
                    continue;

                var marketPayload = ParseMarketPayload(evt.Event.PAYLOAD_JSON);
                if (marketPayload == null || string.IsNullOrEmpty(marketPayload.QuoteToken))
                    continue;

                try
                {
                    var priceUsd = GetSymbolPrice(databaseContext, evt.Chain, evt.Event.DATE_UNIX_SECONDS,
                        marketPayload.QuoteToken, marketPayload.Price, tokenPrices);
                    var endPriceUsd = GetSymbolPrice(databaseContext, evt.Chain, evt.Event.DATE_UNIX_SECONDS,
                        marketPayload.QuoteToken, marketPayload.EndPrice, tokenPrices);

                    if (priceUsd == 0 && endPriceUsd == 0) continue;

                    MarketEventFiatPriceMethods.Upsert(databaseContext, marketEvent, priceUsd, endPriceUsd);
                    pricesProcessed++;
                }
                catch
                {
                    Log.Warning("Event USD price can't be calculated using token price '{Price}'",
                        marketPayload.Price);
                }
            }

            if (pricesProcessed > 0) databaseContext.SaveChanges();
        }

        var processTime = DateTime.Now - startTime;
        if (processTime.TotalSeconds > 1 || pricesProcessed > 0)
        {
            Log.Information(
                "{Name} plugin: Processed events USD prices: {PricesProcessed} processed in {ProcessTime} sec", Name,
                pricesProcessed, Math.Round(processTime.TotalSeconds, 3));
        }
    }


    private static MarketEventData ParseMarketPayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<MarketEventPayload>(payloadJson, PayloadJsonOptions)?.MarketEvent;
        }
        catch
        {
            return null;
        }
    }


    private static decimal GetSymbolPrice(MainDbContext databaseContext, Chain chain,
        long dateUnixSeconds, string quoteTokenSymbol, string priceRaw,
        IEnumerable<TokenMethods.TokenPrice> tokenPrices)
    {
        if (chain == null || string.IsNullOrEmpty(quoteTokenSymbol) || string.IsNullOrEmpty(priceRaw))
            return 0;

        var price = TokenDailyPricesMethods.CalculateAsync(databaseContext, chain,
            dateUnixSeconds, quoteTokenSymbol, priceRaw).Result;

        if (price == 0)
            price = (decimal)TokenMethods.CalculatePrice(tokenPrices, priceRaw, quoteTokenSymbol);

        return price;
    }
}
