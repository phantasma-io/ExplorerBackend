using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Phantasma.Numerics;

namespace Database.Main;

public static class AddressBalanceMethods
{
    public static AddressBalance Upsert(MainDbContext databaseContext, Address address, string chainName, string symbol,
        string amount, bool saveChanges = true)
    {
        var chain = ChainMethods.Get(databaseContext, chainName);
        if ( chain == null ) return null;

        var token = TokenMethods.Get(databaseContext, chain.ID, symbol);
        if ( token == null ) return null;

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

        var currentBalances = databaseContext.AddressBalances.Where(x => x.Address == address);

        var balanceList = new List<AddressBalance>();
        foreach ( var (chainName, symbol, amount) in balances )
        {
            var chain = ChainMethods.Get(databaseContext, chainName);
            if ( chain == null ) continue;

            var token = TokenMethods.Get(databaseContext, chain, symbol);
            if ( token == null ) continue;

            var entry = databaseContext.AddressBalances.FirstOrDefault(x =>
                x.Address == address && x.Chain == chain && x.Token == token);

            var amountConverted =
                UnitConversion.ToDecimal(amount, token.DECIMALS).ToString(CultureInfo.InvariantCulture);
            if ( entry != null )
                entry.AMOUNT = amountConverted;
            else
            {
                entry = new AddressBalance
                {
                    Token = token,
                    Address = address,
                    Chain = chain,
                    AMOUNT = amountConverted
                };
                balanceList.Add(entry);
            }
        }

        databaseContext.AddressBalances.AddRange(balanceList);

        var removeList = new List<AddressBalance>();
        foreach ( var balance in currentBalances )
        {
            if ( balanceList.All(x => x.Token != balance.Token) )
            {
                removeList.Add(balance);
            }
        }

        if ( !removeList.Any() )
        {
            databaseContext.AddressBalances.RemoveRange(removeList);
        }

        if ( saveChanges ) databaseContext.SaveChanges();
    }
}
