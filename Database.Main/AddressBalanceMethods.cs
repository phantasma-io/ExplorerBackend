using System.Linq;

namespace Database.Main;

public static class AddressBalanceMethods
{
    public static AddressBalance Upsert(MainDbContext databaseContext, Address address, string chainName, string symbol,
        string amount, bool saveChanges = true)
    {
        var chain = ChainMethods.Get(databaseContext, chainName);
        var token = TokenMethods.Get(databaseContext, symbol);

        if ( chain == null || token == null ) return null;

        var entry = databaseContext.AddressBalances.FirstOrDefault(x =>
            x.Address == address && x.Chain == chain && x.Token == token);

        if ( entry != null )
            entry.AMOUNT = amount;
        else
        {
            entry = new AddressBalance
            {
                Token = token,
                Chain = chain,
                Address = address,
                AMOUNT = amount
            };
            databaseContext.AddressBalances.Add(entry);
            if ( saveChanges ) databaseContext.SaveChanges();
        }

        return entry;
    }
}
