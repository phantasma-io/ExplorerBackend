namespace Database.Main;

public static class GasEventMethods
{
    public static GasEvent Upsert(MainDbContext databaseContext, string address, string price, string amount,
        Event databaseEvent, Chain chain, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(address) || string.IsNullOrEmpty(price) || string.IsNullOrEmpty(amount) ) return null;

        var addressEntry = AddressMethods.Upsert(databaseContext, chain, address, saveChanges);

        var gasEvent = new GasEvent {Address = addressEntry, PRICE = price, AMOUNT = amount, Event = databaseEvent};

        databaseContext.GasEvents.Add(gasEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return gasEvent;
    }
}
