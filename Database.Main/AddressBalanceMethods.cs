using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

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

        // Build one authoritative set for this chunk:
        // - upsert rows (address, token -> amount),
        // - keep pairs (address, token) used to remove stale balances.
        // Last value wins if payload contains duplicate symbol entries for one address.
        var upsertsByAddressToken = new Dictionary<(int AddressId, int TokenId), (string Amount, BigInteger AmountRaw)>();
        var keepAddressTokenPairs = new HashSet<(int AddressId, int TokenId)>();

        foreach (var (addressId, balanceItems) in balancesByAddressId)
        {
            if (addressId <= 0)
                continue;

            if (balanceItems == null || balanceItems.Count == 0)
                // Explicitly empty payload means "delete all balances for this address".
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
                upsertsByAddressToken[key] = (amountConverted, amountRaw);
                keepAddressTokenPairs.Add(key);
            }
        }

        var dbConnection = (NpgsqlConnection)databaseContext.Database.GetDbConnection();
        if (dbConnection.State != System.Data.ConnectionState.Open)
            await dbConnection.OpenAsync();

        var dbTransaction = databaseContext.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;

        if (upsertsByAddressToken.Count > 0)
        {
            var upsertAddressIds = new int[upsertsByAddressToken.Count];
            var upsertTokenIds = new int[upsertsByAddressToken.Count];
            var upsertAmounts = new string[upsertsByAddressToken.Count];
            var upsertAmountRaws = new string[upsertsByAddressToken.Count];

            var index = 0;
            foreach (var (key, value) in upsertsByAddressToken)
            {
                upsertAddressIds[index] = key.AddressId;
                upsertTokenIds[index] = key.TokenId;
                upsertAmounts[index] = value.Amount;
                upsertAmountRaws[index] = value.AmountRaw.ToString(System.Globalization.CultureInfo.InvariantCulture);
                index++;
            }

            // Set-based upsert keeps write amplification low and avoids EF tracking work
            // when syncing large balance chunks.
            await using var upsertCmd = new NpgsqlCommand(@"
INSERT INTO ""AddressBalances"" (""AddressId"", ""TokenId"", ""AMOUNT"", ""AMOUNT_RAW"")
SELECT row.""AddressId"", row.""TokenId"", row.""AMOUNT"", row.""AMOUNT_RAW""::numeric
FROM UNNEST(@address_ids, @token_ids, @amounts, @amount_raws)
AS row(""AddressId"", ""TokenId"", ""AMOUNT"", ""AMOUNT_RAW"")
ON CONFLICT (""AddressId"", ""TokenId"")
DO UPDATE SET
    ""AMOUNT"" = EXCLUDED.""AMOUNT"",
    ""AMOUNT_RAW"" = EXCLUDED.""AMOUNT_RAW"";
", dbConnection, dbTransaction);

            upsertCmd.Parameters.Add("@address_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = upsertAddressIds;
            upsertCmd.Parameters.Add("@token_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = upsertTokenIds;
            upsertCmd.Parameters.Add("@amounts", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = upsertAmounts;
            upsertCmd.Parameters.Add("@amount_raws", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = upsertAmountRaws;

            await upsertCmd.ExecuteNonQueryAsync();
        }

        var keepAddressIds = new int[keepAddressTokenPairs.Count];
        var keepTokenIds = new int[keepAddressTokenPairs.Count];
        var keepIndex = 0;
        foreach (var keepPair in keepAddressTokenPairs)
        {
            keepAddressIds[keepIndex] = keepPair.AddressId;
            keepTokenIds[keepIndex] = keepPair.TokenId;
            keepIndex++;
        }

        // Remove stale rows for all addresses in this chunk.
        // If an address has no keep-pairs (explicit empty payload), this deletes all its balances.
        await using var deleteCmd = new NpgsqlCommand(@"
DELETE FROM ""AddressBalances"" AS balance_row
WHERE balance_row.""AddressId"" = ANY(@target_address_ids)
  AND NOT EXISTS (
      SELECT 1
      FROM UNNEST(@keep_address_ids, @keep_token_ids) AS keep(""AddressId"", ""TokenId"")
      WHERE keep.""AddressId"" = balance_row.""AddressId""
        AND keep.""TokenId"" = balance_row.""TokenId""
  );
", dbConnection, dbTransaction);

        deleteCmd.Parameters.Add("@target_address_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = addressIds.ToArray();
        deleteCmd.Parameters.Add("@keep_address_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = keepAddressIds;
        deleteCmd.Parameters.Add("@keep_token_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = keepTokenIds;

        await deleteCmd.ExecuteNonQueryAsync();
    }
}
