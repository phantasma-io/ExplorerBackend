namespace Database.Main;

public static class AddressEventMethods
{
    public static AddressEvent Upsert(MainDbContext databaseContext, string address, Event databaseEvent, int chainId,
        bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(address) ) return null;

        var addressEntry = AddressMethods.Upsert(databaseContext, chainId, address, false);

        var addressEvent = new AddressEvent
            {Address = addressEntry, Event = databaseEvent};

        databaseContext.AddressEvents.Add(addressEvent);
        if ( saveChanges ) databaseContext.SaveChanges();

        return addressEvent;
    }
}
