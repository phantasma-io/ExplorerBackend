namespace Database.Main;

public static class TransactionSettleEventMethods
{
    public static TransactionSettleEvent Upsert(MainDbContext databaseContext, string hash, string platformName,
        string platformChain, Event databaseEvent, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(platformName) || string.IsNullOrEmpty(platformChain) ) return null;

        //var platform = PlatformMethods.Get(databaseContext, platformName, platformChain);
        //TODO for now
        var platform = PlatformMethods.Get(databaseContext, platformName);

        var transactionSettleEvent = new TransactionSettleEvent
            {HASH = hash, Platform = platform, Event = databaseEvent};

        databaseContext.TransactionSettleEvents.Add(transactionSettleEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return transactionSettleEvent;
    }
}
