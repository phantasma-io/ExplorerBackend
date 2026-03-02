using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Backend.PluginEngine;
using Database.Main;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Serilog;

namespace Backend.Blockchain;

public partial class BlockchainCommonPlugin : Plugin, IDBAccessPlugin
{
    private const string BurnCursorKeyPrefix = "BLOCKCHAIN_COMMON_BURN_CURSOR_EVENT_ID";

    private void MarkBurnedNfts()
    {
        var startTime = DateTime.Now;

        long markedEventCount = 0;
        long markedNftCount = 0;
        var activeTokenCount = 0;
        var scannedEventsCount = 0;
        long previousCursor = 0;
        long nextCursor = 0;

        using (var databaseContext = new MainDbContext())
        {
            var chainId = ChainMethods.GetId(databaseContext, "main");
            if (chainId <= 0)
                return;

            // During block catch-up we defer burn marking to reduce write contention on Events/Nfts.
            if (CatchupGateMethods.TryGetCatchupReady(databaseContext, chainId, out var isCatchupReady) &&
                !isCatchupReady)
            {
                if (!_burnDeferredDueCatchup)
                {
                    Log.Information(
                        "{Name} plugin: Deferring burn events processing while explorer catch-up is in progress",
                        Name);
                    _burnDeferredDueCatchup = true;
                }

                return;
            }

            if (_burnDeferredDueCatchup)
            {
                Log.Information(
                    "{Name} plugin: Resuming burn events processing after explorer reached zero-lag",
                    Name);
                _burnDeferredDueCatchup = false;
            }

            var burnEventId = databaseContext.EventKinds
                .Where(x => x.ChainId == chainId && x.NAME == "TokenBurn")
                .Select(x => x.ID)
                .FirstOrDefault();
            if (burnEventId <= 0)
                return;

            var burnCursorKey = $"{BurnCursorKeyPrefix}_{chainId}";
            previousCursor = GlobalVariableMethods.GetLongAsync(databaseContext, burnCursorKey).GetAwaiter().GetResult();

            // Process burns in bounded cursor chunks.
            // 2000-sized batches can exceed default DB command timeout on dense histories;
            // use smaller slices to keep each cycle deterministic and avoid stream timeouts.
            const int burnEventsBatchSize = 500;

            // TODO: instead of checking for "KCAL", we must add fungible flag to "Contracts" table.
            // Keep the behavior equivalent to previous code (exclude KCAL burns, which are fungible).
            var kcalContractIds = databaseContext.Contracts
                .AsNoTracking()
                .Where(x => x.ChainId == chainId && x.SYMBOL == "KCAL")
                .Select(x => x.ID)
                .ToList();
            var kcalContractIdSet = kcalContractIds.Count == 0
                ? null
                : new HashSet<int>(kcalContractIds);

            var burnEventsBatch = databaseContext.Events
                .AsNoTracking()
                .Where(x => x.EventKindId == burnEventId && x.ID > previousCursor)
                .OrderBy(x => x.ID)
                .Select(x => new { x.ID, x.ContractId, x.TOKEN_ID })
                .Take(burnEventsBatchSize)
                .ToList();

            scannedEventsCount = burnEventsBatch.Count;
            if (burnEventsBatch.Count == 0)
            {
                return;
            }

            nextCursor = burnEventsBatch[^1].ID;

            var burnedTokens = burnEventsBatch
                .Where(x => kcalContractIdSet == null || !kcalContractIdSet.Contains(x.ContractId))
                .Select(x => new { x.ContractId, x.TOKEN_ID })
                .Distinct()
                .ToList();

            if (burnedTokens.Count > 0)
            {
                var contractIds = new int[burnedTokens.Count];
                var tokenIds = new string[burnedTokens.Count];
                for (var index = 0; index < burnedTokens.Count; index++)
                {
                    contractIds[index] = burnedTokens[index].ContractId;
                    tokenIds[index] = burnedTokens[index].TOKEN_ID;
                }

                var dbConnection = (NpgsqlConnection)databaseContext.Database.GetDbConnection();
                if (dbConnection.State != ConnectionState.Open)
                    dbConnection.Open();

                // Keep one set-based SQL batch for both tables so we avoid per-token loops and
                // avoid extra EXISTS probes that can dominate runtime on heavy histories.
                using var updateCommand = new NpgsqlCommand(@"
WITH burned_tokens AS (
    SELECT DISTINCT token.""ContractId"", token.""TokenId""
    FROM UNNEST(@contract_ids, @token_ids) AS token(""ContractId"", ""TokenId"")
),
updated_events AS (
    UPDATE ""Events"" e
    SET ""BURNED"" = TRUE
    FROM burned_tokens bt
    WHERE e.""ContractId"" = bt.""ContractId""
      AND e.""TOKEN_ID"" IS NOT DISTINCT FROM bt.""TokenId""
      AND e.""BURNED"" IS DISTINCT FROM TRUE
    RETURNING e.""ContractId"", e.""TOKEN_ID""
),
updated_nfts AS (
    UPDATE ""Nfts"" n
    SET ""BURNED"" = TRUE
    FROM burned_tokens bt
    WHERE n.""ContractId"" = bt.""ContractId""
      AND n.""TOKEN_ID"" IS NOT DISTINCT FROM bt.""TokenId""
      AND n.""BURNED"" IS DISTINCT FROM TRUE
    RETURNING n.""ContractId"", n.""TOKEN_ID""
),
affected_tokens AS (
    SELECT DISTINCT ue.""ContractId"", ue.""TOKEN_ID"" AS ""TokenId""
    FROM updated_events ue
    UNION
    SELECT DISTINCT un.""ContractId"", un.""TOKEN_ID"" AS ""TokenId""
    FROM updated_nfts un
)
SELECT
    COALESCE((SELECT COUNT(*) FROM affected_tokens), 0)::bigint AS ""ActiveTokenCount"",
    COALESCE((SELECT COUNT(*) FROM updated_events), 0)::bigint AS ""MarkedEventCount"",
    COALESCE((SELECT COUNT(*) FROM updated_nfts), 0)::bigint AS ""MarkedNftCount"";
", dbConnection);
                updateCommand.CommandTimeout = 120;

                updateCommand.Parameters.Add("@contract_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = contractIds;
                updateCommand.Parameters.Add("@token_ids", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = tokenIds;

                using var reader = updateCommand.ExecuteReader();
                if (reader.Read())
                {
                    activeTokenCount = (int)reader.GetInt64(0);
                    markedEventCount = reader.GetInt64(1);
                    markedNftCount = reader.GetInt64(2);
                }
            }

            GlobalVariableMethods.UpsertAsync(databaseContext, burnCursorKey, nextCursor, saveChanges: false)
                .GetAwaiter().GetResult();
            databaseContext.SaveChanges();
        }

        var processTime = DateTime.Now - startTime;
        if (processTime.TotalSeconds > 1 || markedEventCount > 0 || markedNftCount > 0 || scannedEventsCount > 0)
        {
            Log.Information(
                "{Name} plugin: Burned token events processing took {ProcessTime} sec, scanned={ScannedEventsCount}, active_tokens={ActiveTokenCount}, cursor={PreviousCursor}->{NextCursor}, {MarkedEventCount} events marked, {MarkedNftCount} NFTs marked",
                Name, Math.Round(processTime.TotalSeconds, 3), scannedEventsCount, activeTokenCount, previousCursor,
                nextCursor, markedEventCount, markedNftCount);
        }
    }
}
