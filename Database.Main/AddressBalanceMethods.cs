using System;
using System.Collections.Generic;
using System.Linq;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class AddressBalanceMethods
{
    public static void InsertOrUpdateList(MainDbContext databaseContext, Address address,
        List<Tuple<string, string, string>> balances)
    {
        var currentBalances = databaseContext.AddressBalances.Where(x => x.Address == address);

        var balanceListToAdd = new List<AddressBalance>();
        var balanceListAll = new List<AddressBalance>();
        foreach ( var (chainName, symbol, amount) in balances )
        {
            var chain = ChainMethods.Get(databaseContext, chainName);
            if ( chain == null ) continue;

            // TODO async
            var token = TokenMethods.GetAsync(databaseContext, chain, symbol).Result;
            if ( token == null ) continue;

            var entry = databaseContext.AddressBalances.FirstOrDefault(x =>
                x.Address == address && x.Token == token);

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
                    AMOUNT = amountConverted,
                    AMOUNT_RAW = amount
                };
                balanceListToAdd.Add(entry);
            }
            balanceListAll.Add(entry);
        }

        databaseContext.AddressBalances.AddRange(balanceListToAdd);

        var removeList = new List<AddressBalance>();
        foreach ( var balance in currentBalances.Include(x => x.Token) )
            if ( balanceListAll.All(x => x.Token != balance.Token) )
                removeList.Add(balance);

        if ( removeList.Any() ) databaseContext.AddressBalances.RemoveRange(removeList);
    }
}
