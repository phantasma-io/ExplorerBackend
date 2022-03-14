namespace Database.Main;

public static class ChainEventMethods
{
    public static ChainEvent Upsert(MainDbContext databaseContext, string name, string value, Chain chain,
        Event databaseEvent, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value) ) return null;

        var chainEvent = new ChainEvent {NAME = name, VALUE = value, Chain = chain, Event = databaseEvent};

        databaseContext.ChainEvents.Add(chainEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return chainEvent;
    }
}
