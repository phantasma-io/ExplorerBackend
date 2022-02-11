namespace Database.Main;

public static class SaleEventMethods
{
    public static SaleEvent Upsert(MainDbContext databaseContext, string saleKind, string hash, int chainId,
        Event databaseEvent, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(saleKind) || string.IsNullOrEmpty(hash) ) return null;

        var saleEventKind = SaleEventKindMethods.Upsert(databaseContext, saleKind, chainId, saveChanges);

        var saleEvent = new SaleEvent {SaleEventKind = saleEventKind, HASH = hash, Event = databaseEvent};

        databaseContext.SaleEvents.Add(saleEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return saleEvent;
    }
}
