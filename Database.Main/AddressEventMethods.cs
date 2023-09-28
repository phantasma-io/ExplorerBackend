using System.Threading.Tasks;

namespace Database.Main;

public static class AddressEventMethods
{
    public static async Task<AddressEvent> UpsertAsync(MainDbContext databaseContext, string address, Event databaseEvent, Chain chain)
    {
        if ( string.IsNullOrEmpty(address) ) return null;

        var addressEntry = await AddressMethods.UpsertAsync(databaseContext, chain, address);

        var addressEvent = new AddressEvent
            {Address = addressEntry, Event = databaseEvent};

        databaseContext.AddressEvents.Add(addressEvent);

        return addressEvent;
    }
}
