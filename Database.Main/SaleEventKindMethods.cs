using System.Linq;

namespace Database.Main;

public static class SaleEventKindMethods
{
    public static SaleEventKind Upsert(MainDbContext databaseContext, string name, Chain chain, bool saveChanges = true)
    {
        var saleEventKind = databaseContext.SaleEventKinds
            .FirstOrDefault(x => x.NAME == name && x.Chain == chain);
        if ( saleEventKind != null )
            return saleEventKind;

        saleEventKind = DbHelper.GetTracked<SaleEventKind>(databaseContext)
            .FirstOrDefault(x => x.NAME == name && x.Chain == chain);
        if ( saleEventKind != null )
            return saleEventKind;

        saleEventKind = new SaleEventKind {NAME = name, Chain = chain};

        databaseContext.SaleEventKinds.Add(saleEventKind);
        if ( saveChanges ) databaseContext.SaveChanges();

        return saleEventKind;
    }
}
