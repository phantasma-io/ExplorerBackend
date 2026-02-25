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
        if (address == null)
            return;

        var chainId = address.ChainId != 0 ? address.ChainId : address.Chain?.ID ?? 0;
        if (chainId == 0)
            return;

        var addressId = address.ID;
        var currentBalances = addressId > 0
            ? await databaseContext.AddressBalances
                .Where(x => x.AddressId == addressId)
                .ToListAsync()
            : DbHelper.GetTracked<AddressBalance>(databaseContext)
                .Where(x => x.Address == address)
                .ToList();

        var symbols = balances
            .Select(x => x.Item2)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Resolve all tokens for this address chain in one query instead of per-balance point reads.
        var tokensBySymbol = await databaseContext.Tokens
            .Where(x => x.ChainId == chainId && symbols.Contains(x.SYMBOL))
            .ToDictionaryAsync(x => x.SYMBOL, x => x, StringComparer.Ordinal);

        var currentByTokenId = currentBalances
            .GroupBy(x => x.TokenId)
            .ToDictionary(x => x.Key, x => x.First());

        var balanceListToAdd = new List<AddressBalance>();
        var keepTokenIds = new HashSet<int>();

        foreach (var (_, symbol, amount) in balances)
        {
            if (!tokensBySymbol.TryGetValue(symbol, out var token))
                continue;

            var amountRaw = BigInteger.TryParse(amount, out var parsedAmount) ? parsedAmount : BigInteger.Zero;
            var amountConverted = Utils.ToDecimal(amount, token.DECIMALS);

            if (currentByTokenId.TryGetValue(token.ID, out var existingBalance))
            {
                existingBalance.AMOUNT = amountConverted;
                existingBalance.AMOUNT_RAW = amountRaw;
            }
            else
            {
                var newBalance = new AddressBalance
                {
                    Token = token,
                    Address = address,
                    AMOUNT = amountConverted,
                    AMOUNT_RAW = amountRaw
                };
                balanceListToAdd.Add(newBalance);
                currentByTokenId[token.ID] = newBalance;
            }

            keepTokenIds.Add(token.ID);
        }

        if (balanceListToAdd.Count > 0)
            await databaseContext.AddressBalances.AddRangeAsync(balanceListToAdd);

        var removeList = currentBalances.Where(x => !keepTokenIds.Contains(x.TokenId)).ToList();

        if (removeList.Count > 0)
            databaseContext.AddressBalances.RemoveRange(removeList);
    }
}
