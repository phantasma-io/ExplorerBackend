using System.Linq;

namespace Database.Main;

public static class MarketEventFiatPriceMethods
{
    public static MarketEventFiatPrice Upsert(MainDbContext databaseContext, MarketEvent marketEvent, decimal priceUsd,
        decimal priceEndUsd, string fiatName, bool saveChanges = true)
    {
        if ( marketEvent is null ) return null;

        var marketEventFiatPrice =
            databaseContext.MarketEventFiatPrices.FirstOrDefault(x => x.MarketEventId == marketEvent.ID);
        //already inserted, update values

        if ( marketEventFiatPrice == null )
        {
            marketEventFiatPrice = new MarketEventFiatPrice
            {
                MarketEvent = marketEvent,
                PRICE_USD = priceUsd,
                PRICE_END_USD = priceEndUsd,
                FIAT_NAME = fiatName
            };

            databaseContext.MarketEventFiatPrices.Add(marketEventFiatPrice);
        }
        else
        {
            marketEventFiatPrice.PRICE_USD = priceUsd;
            marketEventFiatPrice.PRICE_END_USD = priceEndUsd;
            marketEventFiatPrice.FIAT_NAME = fiatName;
        }

        if ( saveChanges ) databaseContext.SaveChanges();

        return marketEventFiatPrice;
    }
}
