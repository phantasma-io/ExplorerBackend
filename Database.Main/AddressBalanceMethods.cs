using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class AddressBalanceMethods
{
    public static async Task InsertOrUpdateList(MainDbContext databaseContext, Address address,
        List<Tuple<string, string, string>> balances)
    {
        var currentBalances = databaseContext.AddressBalances.Where(x => x.Address == address);

        var balanceListToAdd = new List<AddressBalance>();
        var balanceListAll = new List<AddressBalance>();
        foreach ( var (chainName, symbol, amount) in balances )
        {
            var chain = await ChainMethods.GetAsync(databaseContext, chainName);
            if ( chain == null ) continue;

            var token = await TokenMethods.GetAsync(databaseContext, chain, symbol);
            if ( token == null ) continue;

            var entry = await databaseContext.AddressBalances.FirstOrDefaultAsync(x =>
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

        await databaseContext.AddressBalances.AddRangeAsync(balanceListToAdd);

        var removeList = currentBalances.Include(x => x.Token).Where(tokenBalance => balanceListAll.All(x => x.Token != tokenBalance.Token)).ToList();

        if ( removeList.Any() ) databaseContext.AddressBalances.RemoveRange(removeList);
    }
}
