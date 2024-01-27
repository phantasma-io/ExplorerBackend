using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class MarketEventKindMethods
{
    public static async Task<MarketEventKind> UpsertAsync(MainDbContext databaseContext, string name, Chain chain)
    {
        var marketEventKind = await databaseContext.MarketEventKinds
            .FirstOrDefaultAsync(x => x.NAME == name && x.Chain == chain);
        if ( marketEventKind != null ) return marketEventKind;

        marketEventKind = DbHelper.GetTracked<MarketEventKind>(databaseContext)
            .FirstOrDefault(x => x.NAME == name && x.Chain == chain);
        if ( marketEventKind != null ) return marketEventKind;

        marketEventKind = new MarketEventKind {NAME = name, Chain = chain};

        await databaseContext.MarketEventKinds.AddAsync(marketEventKind);

        return marketEventKind;
    }
}
