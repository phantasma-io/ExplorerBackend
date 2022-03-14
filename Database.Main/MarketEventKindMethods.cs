using System.Linq;

namespace Database.Main;

public static class MarketEventKindMethods
{
    public static MarketEventKind Upsert(MainDbContext databaseContext, string name, Chain chain,
        bool saveChanges = true)
    {
        var marketEventKind = databaseContext.MarketEventKinds
            .FirstOrDefault(x => x.NAME == name && x.Chain == chain);
        if ( marketEventKind != null ) return marketEventKind;

        marketEventKind = DbHelper.GetTracked<MarketEventKind>(databaseContext)
            .FirstOrDefault(x => x.NAME == name && x.Chain == chain);
        if ( marketEventKind != null ) return marketEventKind;

        marketEventKind = new MarketEventKind {NAME = name, Chain = chain};

        databaseContext.MarketEventKinds.Add(marketEventKind);
        if ( saveChanges ) databaseContext.SaveChanges();

        return marketEventKind;
    }
}
