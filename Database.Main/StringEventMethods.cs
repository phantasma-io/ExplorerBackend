namespace Database.Main;

public static class StringEventMethods
{
    public static StringEvent Upsert(MainDbContext databaseContext, string value, Event databaseEvent,
        bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(value) ) return null;

        var stringEvent = new StringEvent {STRING_VALUE = value, Event = databaseEvent};

        databaseContext.StringEvents.Add(stringEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return stringEvent;
    }
}
