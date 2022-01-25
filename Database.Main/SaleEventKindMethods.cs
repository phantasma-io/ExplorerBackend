using System.Linq;

namespace Database.Main;

public static class SaleEventKindMethods
{
    public static SaleEventKind Upsert(MainDbContext databaseContext, string name, int chainId, bool saveChanges = true)
    {
        var saleEventKind = databaseContext.SaleEventKinds
            .FirstOrDefault(x => string.Equals(x.NAME.ToUpper(), name.ToUpper()) && x.ChainId == chainId);

        if ( saleEventKind != null )
            return saleEventKind;

        var chain = ChainMethods.Get(databaseContext, chainId);

        saleEventKind = new SaleEventKind {NAME = name, Chain = chain};

        databaseContext.SaleEventKinds.Add(saleEventKind);
        if ( saveChanges ) databaseContext.SaveChanges();

        return saleEventKind;
    }
}
