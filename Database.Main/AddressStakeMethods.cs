using System.Linq;

namespace Database.Main;

public static class AddressStakeMethods
{
    public static AddressStake Upsert(MainDbContext databaseContext, Address address, string amount, string amount_raw,
        long time,
        string unclaimed, string unclaimed_raw, bool saveChanges = true)
    {
        var entry = databaseContext.AddressStakes.FirstOrDefault(x => x.Address == address);

        if ( entry == null )
        {
            entry = new AddressStake
            {
                Address = address,
                AMOUNT = amount,
                AMOUNT_RAW = amount_raw,
                TIME = time,
                UNCLAIMED = unclaimed,
                UNCLAIMED_RAW = unclaimed_raw
            };
            databaseContext.AddressStakes.Add(entry);
            if ( saveChanges ) databaseContext.SaveChanges();
        }
        else
        {
            entry.TIME = time;
            entry.AMOUNT = amount;
            entry.AMOUNT_RAW = amount_raw;
            entry.UNCLAIMED = unclaimed;
            entry.UNCLAIMED_RAW = unclaimed_raw;
        }

        return entry;
    }
}
