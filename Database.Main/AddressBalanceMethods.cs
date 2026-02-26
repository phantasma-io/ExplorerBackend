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

        if (address.ID <= 0)
            return;

        // Null/empty balances are a valid sync result for an address.
        // We must forward that state to batch sync so stale rows are removed.
        var normalized = balances == null
            ? new List<(string Symbol, string AmountRaw)>()
            : balances
                .Where(x => !string.IsNullOrWhiteSpace(x.Item2) && !string.IsNullOrWhiteSpace(x.Item3))
                .Select(x => (x.Item2, x.Item3))
                .ToList();

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

        Dictionary<string, Token> tokensBySymbol;
        if (symbols.Count > 0)
        {
            // Resolve all token ids once for this chunk.
            tokensBySymbol = await databaseContext.Tokens
                .Where(x => x.ChainId == chainId && symbols.Contains(x.SYMBOL))
                .ToDictionaryAsync(x => x.SYMBOL, x => x, StringComparer.Ordinal);
        }
        else
        {
            // No symbols in payload still means we might need deletions for addresses
            // that were returned with an explicit empty balance list.
            tokensBySymbol = new Dictionary<string, Token>(StringComparer.Ordinal);
        }

        // Load existing balances for the whole chunk once and merge in-memory.
        var currentBalances = await databaseContext.AddressBalances
            .Where(x => addressIds.Contains(x.AddressId))
            .ToListAsync();

        var currentByAddressToken = currentBalances
            .GroupBy(x => (x.AddressId, x.TokenId))
            .ToDictionary(x => x.Key, x => x.First());

        // Initialize keep-sets for every requested address.
        // Empty set means "address is known in this batch and should end up with zero balances".
        var keepTokenIdsByAddress = addressIds.ToDictionary(x => x, _ => new HashSet<int>());
        var toInsert = new List<AddressBalance>();

        foreach (var (addressId, balanceItems) in balancesByAddressId)
        {
            if (addressId <= 0)
                continue;

            var keepTokenIds = keepTokenIdsByAddress[addressId];
            if (balanceItems == null || balanceItems.Count == 0)
                // Explicitly empty payload keeps the set empty, so all existing balances are removed below.
                continue;

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

        // Remove balances not present in the authoritative keep-set per address.
        // For addresses with explicit empty balance payload this removes all current rows.
        var toRemove = currentBalances
            .Where(x => keepTokenIdsByAddress.TryGetValue(x.AddressId, out var keep) && !keep.Contains(x.TokenId))
            .ToList();

        if (toRemove.Count > 0)
            databaseContext.AddressBalances.RemoveRange(toRemove);
    }
}
