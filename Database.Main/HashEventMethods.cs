namespace Database.Main;

public static class HashEventMethods
{
    public static HashEvent Upsert(MainDbContext databaseContext, string hash, Event databaseEvent)
    {
        if ( string.IsNullOrEmpty(hash) ) return null;

        var hashEvent = new HashEvent {HASH = hash, Event = databaseEvent};

        databaseContext.HashEvents.Add(hashEvent);

        return hashEvent;
    }
}
