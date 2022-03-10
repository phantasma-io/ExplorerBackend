using System;
using System.Collections.Generic;
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


    public static void InsertOrUpdateList(MainDbContext databaseContext, Address address,
        List<Tuple<string, string, string>> balances, bool saveChanges = true)
    {
        if ( !balances.Any() || address == null ) return;

        var balanceList = new List<AddressBalance>();
        foreach ( var (chainName, symbol, amount) in balances )
        {
            var chain = ChainMethods.Get(databaseContext, chainName);
            var token = TokenMethods.Get(databaseContext, symbol);

            if ( chain == null || token == null ) continue;

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
                balanceList.Add(entry);
            }
        }

        databaseContext.AddressBalances.AddRange(balanceList);
        if ( saveChanges ) databaseContext.SaveChanges();
    }
}
