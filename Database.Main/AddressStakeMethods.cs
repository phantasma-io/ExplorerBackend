using System.Linq;

namespace Database.Main;

public static class AddressStakeMethods
{
    public static AddressStake Upsert(MainDbContext databaseContext, Address address, string amount, long time,
        string unclaimed, bool saveChanges = true)
    {
        var entry = databaseContext.AddressStakes.FirstOrDefault(x => x.Address == address);

        if ( entry == null )
        {
            entry = new AddressStake
            {
                Address = address,
                AMOUNT = amount,
                TIME = time,
                UNCLAIMED = unclaimed
            };
            databaseContext.AddressStakes.Add(entry);
            if ( saveChanges ) databaseContext.SaveChanges();
        }
        else
        {
            entry.TIME = time;
            entry.AMOUNT = amount;
            entry.UNCLAIMED = unclaimed;
        }

        return entry;
    }
}
