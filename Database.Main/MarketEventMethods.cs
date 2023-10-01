using System.Threading.Tasks;

namespace Database.Main;

public static class MarketEventMethods
{
    public static async Task InsertAsync(MainDbContext databaseContext, string marketKind, string baseSymbol,
        string quoteSymbol, string price, string endPrice, string marketId, Chain chain, Event databaseEvent)
    {
        if ( string.IsNullOrEmpty(marketKind) || string.IsNullOrEmpty(baseSymbol) ||
             string.IsNullOrEmpty(quoteSymbol) ) return;

        var baseToken = await TokenMethods.GetAsync(databaseContext, chain, baseSymbol);
        var quoteToken = await TokenMethods.GetAsync(databaseContext, chain, quoteSymbol);

        var marketEventKind = await MarketEventKindMethods.UpsertAsync(databaseContext, marketKind, chain);

        var marketEvent = new MarketEvent
        {
            BaseToken = baseToken,
            QuoteToken = quoteToken,
            PRICE = price,
            END_PRICE = endPrice,
            MARKET_ID = marketId,
            MarketEventKind = marketEventKind,
            Event = databaseEvent
        };

        await databaseContext.MarketEvents.AddAsync(marketEvent);
    }
}
