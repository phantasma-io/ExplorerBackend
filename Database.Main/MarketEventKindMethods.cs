using System.Linq;

namespace Database.Main;

public static class MarketEventKindMethods
{
    public static MarketEventKind Upsert(MainDbContext databaseContext, string name, int chainId,
        bool saveChanges = true)
    {
        var marketEventKind = databaseContext.MarketEventKinds
            .FirstOrDefault(x => string.Equals(x.NAME.ToUpper(), name.ToUpper()) && x.ChainId == chainId);

        if ( marketEventKind != null )
            return marketEventKind;

        var chain = ChainMethods.Get(databaseContext, chainId);

        marketEventKind = new MarketEventKind {NAME = name, Chain = chain};

        databaseContext.MarketEventKinds.Add(marketEventKind);
        if ( saveChanges ) databaseContext.SaveChanges();

        return marketEventKind;
    }
}
