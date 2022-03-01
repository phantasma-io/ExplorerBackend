using System.Linq;

namespace Database.Main;

public static class AddressStorageMethods
{
    public static AddressStorage Upsert(MainDbContext databaseContext, Address address, long available, long used,
        string avatar, bool saveChanges = true)
    {
        var entry = databaseContext.AddressStorages.FirstOrDefault(x => x.Address == address);

        if ( entry == null )
        {
            entry = new AddressStorage
            {
                Address = address,
                AVAILABLE = available,
                USED = used,
                AVATAR = avatar
            };
            databaseContext.AddressStorages.Add(entry);
            if ( saveChanges ) databaseContext.SaveChanges();
        }
        else
        {
            entry.AVAILABLE = available;
            entry.USED = used;
            entry.AVATAR = avatar;
        }

        return entry;
    }
}
