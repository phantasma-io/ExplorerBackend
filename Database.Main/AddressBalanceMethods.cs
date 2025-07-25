using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
            var token = await TokenMethods.GetAsync(databaseContext, address.Chain, symbol);
            if ( token == null ) continue;

            var entry = await databaseContext.AddressBalances.FirstOrDefaultAsync(x =>
                x.Address == address && x.Token == token);

            var amountConverted = Utils.ToDecimal(amount, token.DECIMALS);
            if ( entry != null )
            {
                entry.AMOUNT = amountConverted;
                entry.AMOUNT_RAW = BigInteger.TryParse(amount, out var result) ? result : BigInteger.Zero;
            }
            else
            {
                entry = new AddressBalance
                {
                    Token = token,
                    Address = address,
                    AMOUNT = amountConverted,
                    AMOUNT_RAW = BigInteger.TryParse(amount, out var result) ? result : BigInteger.Zero
                };
                balanceListToAdd.Add(entry);
            }
            balanceListAll.Add(entry);
        }

        await databaseContext.AddressBalances.AddRangeAsync(balanceListToAdd);

        var removeList = currentBalances
            .Where(tokenBalance => !balanceListAll.Select(x => x.TokenId).Contains(tokenBalance.TokenId))
            .ToList();

        if ( removeList.Any() ) databaseContext.AddressBalances.RemoveRange(removeList);
    }
}
