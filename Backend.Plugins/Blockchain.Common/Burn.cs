using System;
using System.Collections.Generic;
using System.Linq;
using Backend.PluginEngine;
using Database.Main;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Blockchain;

public partial class BlockchainCommonPlugin : Plugin, IDBAccessPlugin
{
    private const string BurnCursorKeyPrefix = "BLOCKCHAIN_COMMON_BURN_CURSOR_EVENT_ID";

    private void MarkBurnedNfts()
    {
        var startTime = DateTime.Now;

        var markedEventCount = 0;
        var markedNftCount = 0;
        var scannedEventsCount = 0;
        long previousCursor = 0;
        long nextCursor = 0;

        using (var databaseContext = new MainDbContext())
        {
            var chainId = ChainMethods.GetId(databaseContext, "main");
            if (chainId <= 0)
                return;

            var burnEventId = databaseContext.EventKinds
                .Where(x => x.ChainId == chainId && x.NAME == "TokenBurn")
                .Select(x => x.ID)
                .FirstOrDefault();
            if (burnEventId <= 0)
                return;

            var burnCursorKey = $"{BurnCursorKeyPrefix}_{chainId}";
            previousCursor = GlobalVariableMethods.GetLongAsync(databaseContext, burnCursorKey).GetAwaiter().GetResult();

            // Process burns in cursor-ordered chunks to avoid full-table rescans every interval.
            const int burnEventsBatchSize = 2000;

            // TODO: instead of checking for "KCAL", we must add fungible flag to "Contracts" table.
            // Keep the behavior equivalent to previous code (exclude KCAL burns, which are fungible).
            var kcalContractIds = databaseContext.Contracts
                .AsNoTracking()
                .Where(x => x.ChainId == chainId && x.SYMBOL == "KCAL")
                .Select(x => x.ID)
                .ToList();

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
                .Where(x => !kcalContractIds.Contains(x.ContractId))
                .Select(x => new { x.ContractId, x.TOKEN_ID })
                .Distinct()
                .ToList();

            foreach (var burnedToken in burnedTokens)
            {
                // Mark all events for this token as burned.
                // Use `IS NOT DISTINCT FROM` to correctly match NULL token ids if they exist (SQL '=' does not).
                markedEventCount += databaseContext.Database.ExecuteSqlInterpolated(
                    $@"UPDATE ""Events"" SET ""BURNED"" = TRUE
                       WHERE ""ContractId"" = {burnedToken.ContractId}
                         AND ""TOKEN_ID"" IS NOT DISTINCT FROM {burnedToken.TOKEN_ID}
                         AND ""BURNED"" IS DISTINCT FROM TRUE;");

                // Mark the NFT row as burned if it exists (for fungible burns this will affect 0 rows).
                markedNftCount += databaseContext.Database.ExecuteSqlInterpolated(
                    $@"UPDATE ""Nfts"" SET ""BURNED"" = TRUE
                       WHERE ""ContractId"" = {burnedToken.ContractId}
                         AND ""TOKEN_ID"" IS NOT DISTINCT FROM {burnedToken.TOKEN_ID}
                         AND ""BURNED"" IS DISTINCT FROM TRUE;");
            }

            GlobalVariableMethods.UpsertAsync(databaseContext, burnCursorKey, nextCursor, saveChanges: false)
                .GetAwaiter().GetResult();
            databaseContext.SaveChanges();
        }

        var processTime = DateTime.Now - startTime;
        if (processTime.TotalSeconds > 1 || markedEventCount > 0 || markedNftCount > 0 || scannedEventsCount > 0)
        {
            Log.Information(
                "{Name} plugin: Burned token events processing took {ProcessTime} sec, scanned={ScannedEventsCount}, cursor={PreviousCursor}->{NextCursor}, {MarkedEventCount} events marked, {MarkedNftCount} NFTs marked",
                Name, Math.Round(processTime.TotalSeconds, 3), scannedEventsCount, previousCursor, nextCursor,
                markedEventCount, markedNftCount);
        }
    }
}
