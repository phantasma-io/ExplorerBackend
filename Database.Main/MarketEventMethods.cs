namespace Database.Main;

public static class MarketEventMethods
{
    public static MarketEvent Upsert(MainDbContext databaseContext, string marketKind, string baseSymbol,
        string quoteSymbol, string price, string endPrice, string marketId, Chain chain, Event databaseEvent,
        bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(marketKind) || string.IsNullOrEmpty(baseSymbol) ||
             string.IsNullOrEmpty(quoteSymbol) ) return null;

        var baseToken = TokenMethods.Get(databaseContext, chain, baseSymbol);
        var quoteToken = TokenMethods.Get(databaseContext, chain, quoteSymbol);

        var marketEventKind = MarketEventKindMethods.Upsert(databaseContext, marketKind, chain, saveChanges);

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

        databaseContext.MarketEvents.Add(marketEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return marketEvent;
    }
}
