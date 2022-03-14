namespace Database.Main;

public static class AddressEventMethods
{
    public static AddressEvent Upsert(MainDbContext databaseContext, string address, Event databaseEvent, Chain chain,
        bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(address) ) return null;

        var addressEntry = AddressMethods.Upsert(databaseContext, chain, address, saveChanges);

        var addressEvent = new AddressEvent
            {Address = addressEntry, Event = databaseEvent};

        databaseContext.AddressEvents.Add(addressEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return addressEvent;
    }
}
