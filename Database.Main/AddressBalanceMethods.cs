using System;
using System.Collections.Generic;
using System.Linq;
using Backend.Commons;

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

        var amountConverted = Utils.ToDecimal(amount, token.DECIMALS);

        if ( entry != null )
        {
            entry.AMOUNT = amountConverted;
            entry.AMOUNT_RAW = amount;
        }
        else
        {
            entry = new AddressBalance
            {
                Token = token,
                Chain = chain,
                Address = address,
                AMOUNT = amountConverted,
                AMOUNT_RAW = amount
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

            var amountConverted = Utils.ToDecimal(amount, token.DECIMALS);
            if ( entry != null )
            {
                entry.AMOUNT = amountConverted;
                entry.AMOUNT_RAW = amount;
            }
            else
            {
                entry = new AddressBalance
                {
                    Token = token,
                    Address = address,
                    Chain = chain,
                    AMOUNT = amountConverted,
                    AMOUNT_RAW = amount
                };
                balanceList.Add(entry);
            }
        }

        databaseContext.AddressBalances.AddRange(balanceList);

        var removeList = new List<AddressBalance>();
        foreach ( var balance in currentBalances )
            if ( balanceList.All(x => x.Token != balance.Token) )
                removeList.Add(balance);

        if ( !removeList.Any() ) databaseContext.AddressBalances.RemoveRange(removeList);

        if ( saveChanges ) databaseContext.SaveChanges();
    }
}
