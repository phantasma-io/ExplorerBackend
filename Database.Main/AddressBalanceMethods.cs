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

        if (address.ID <= 0 || balances == null || balances.Count == 0)
            return;

        var normalized = balances
            .Where(x => !string.IsNullOrWhiteSpace(x.Item2) && !string.IsNullOrWhiteSpace(x.Item3))
            .Select(x => (x.Item2, x.Item3))
            .ToList();

        if (normalized.Count == 0)
            return;

        var batch = new Dictionary<int, IReadOnlyList<(string Symbol, string AmountRaw)>> { [address.ID] = normalized };
        await InsertOrUpdateBatchAsync(databaseContext, chainId, batch);
    }

    public static async Task InsertOrUpdateBatchAsync(
        MainDbContext databaseContext,
        int chainId,
        IReadOnlyDictionary<int, IReadOnlyList<(string Symbol, string AmountRaw)>> balancesByAddressId)
    {
        if (chainId == 0 || balancesByAddressId == null || balancesByAddressId.Count == 0)
            return;

        var addressIds = balancesByAddressId.Keys
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (addressIds.Count == 0)
            return;

        var symbols = balancesByAddressId.Values
            .Where(x => x != null)
            .SelectMany(x => x)
            .Select(x => x.Symbol)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (symbols.Count == 0)
            return;

        // Resolve all token ids once for this chunk.
        var tokensBySymbol = await databaseContext.Tokens
            .Where(x => x.ChainId == chainId && symbols.Contains(x.SYMBOL))
            .ToDictionaryAsync(x => x.SYMBOL, x => x, StringComparer.Ordinal);

        // Load existing balances for the whole chunk once and merge in-memory.
        var currentBalances = await databaseContext.AddressBalances
            .Where(x => addressIds.Contains(x.AddressId))
            .ToListAsync();

        var currentByAddressToken = currentBalances
            .GroupBy(x => (x.AddressId, x.TokenId))
            .ToDictionary(x => x.Key, x => x.First());

        var keepTokenIdsByAddress = new Dictionary<int, HashSet<int>>();
        var toInsert = new List<AddressBalance>();

        foreach (var (addressId, balanceItems) in balancesByAddressId)
        {
            if (addressId <= 0 || balanceItems == null || balanceItems.Count == 0)
                continue;

            if (!keepTokenIdsByAddress.TryGetValue(addressId, out var keepTokenIds))
            {
                keepTokenIds = new HashSet<int>();
                keepTokenIdsByAddress[addressId] = keepTokenIds;
            }

            foreach (var (symbol, amountRawString) in balanceItems)
            {
                if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(amountRawString))
                    continue;

                if (!tokensBySymbol.TryGetValue(symbol, out var token))
                    continue;

                var amountRaw = BigInteger.TryParse(amountRawString, out var parsedAmount)
                    ? parsedAmount
                    : BigInteger.Zero;
                var amountConverted = Utils.ToDecimal(amountRawString, token.DECIMALS);
                var key = (addressId, token.ID);

                if (currentByAddressToken.TryGetValue(key, out var existing))
                {
                    existing.AMOUNT = amountConverted;
                    existing.AMOUNT_RAW = amountRaw;
                }
                else
                {
                    var newBalance = new AddressBalance
                    {
                        AddressId = addressId,
                        TokenId = token.ID,
                        AMOUNT = amountConverted,
                        AMOUNT_RAW = amountRaw
                    };

                    toInsert.Add(newBalance);
                    currentByAddressToken[key] = newBalance;
                }

                keepTokenIds.Add(token.ID);
            }
        }

        if (toInsert.Count > 0)
            await databaseContext.AddressBalances.AddRangeAsync(toInsert);

        var toRemove = currentBalances
            .Where(x => keepTokenIdsByAddress.TryGetValue(x.AddressId, out var keep) && !keep.Contains(x.TokenId))
            .ToList();

        if (toRemove.Count > 0)
            databaseContext.AddressBalances.RemoveRange(toRemove);
    }
}
