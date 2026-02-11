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
    private void MarkBurnedNfts()
    {
        var startTime = DateTime.Now;

        var markedEventCount = 0;
        var markedNftCount = 0;

        using (var databaseContext = new MainDbContext())
        {
            int[] chainIds = { ChainMethods.GetId(databaseContext, "main") };

            var burnEventId = databaseContext.EventKinds
                .Where(x => chainIds.Contains(x.ChainId) && x.NAME == "TokenBurn").Select(x => x.ID).First();

            // This routine used to:
            // - run a DISTINCT query over the full Events table,
            // - join Contracts to filter out KCAL by symbol,
            // - then load all events for each (ContractId, TOKEN_ID) pair and SaveChanges().
            //
            // On large DBs the DISTINCT + join query can be slow enough to hit Npgsql stream read timeouts.
            // To make this deterministic:
            // - avoid DISTINCT in SQL (we de-duplicate in memory),
            // - avoid the Contract navigation join (filter by KCAL ContractId),
            // - mark rows using set-based UPDATE statements instead of loading entities.
            //
            // Note: the plugin runs periodically, so we keep per-invocation work bounded.
            const int maxTokensPerInvocation = 500;
            const int burnEventsScanLimit = 2000; // scan more burn events to find enough distinct token pairs

            // TODO: instead of checking for "KCAL", we must add fungible flag to "Contracts" table.
            // Keep the behavior equivalent to previous code (exclude KCAL burns, which are fungible).
            var kcalContractIds = databaseContext.Contracts
                .AsNoTracking()
                .Where(x => chainIds.Contains(x.ChainId) && x.SYMBOL == "KCAL")
                .Select(x => x.ID)
                .ToList();

            var processedTokens = 0;
            while (processedTokens < maxTokensPerInvocation)
            {
                var burnEventsQuery = databaseContext.Events
                    .AsNoTracking()
                    .Where(x => x.EventKindId == burnEventId && x.BURNED != true);

                if (kcalContractIds.Count > 0)
                {
                    burnEventsQuery = burnEventsQuery.Where(x => !kcalContractIds.Contains(x.ContractId));
                }

                var burnedTokens = burnEventsQuery
                    .OrderBy(x => x.ID)
                    .Select(x => new { x.ContractId, x.TOKEN_ID })
                    .Take(burnEventsScanLimit)
                    .ToList()
                    .Distinct()
                    .Take(100)
                    .ToList();

                if (burnedTokens.Count == 0)
                {
                    break;
                }

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

                    processedTokens++;
                    if (processedTokens >= maxTokensPerInvocation)
                        break;
                }
            }
        }

        var processTime = DateTime.Now - startTime;
        if (processTime.TotalSeconds > 1 || markedEventCount > 0 || markedNftCount > 0)
        {
            Log.Information(
                "{Name} plugin: Burned token events processing took {ProcessTime} sec, {MarkedEventCount} events marked, {MarkedNftCount} NFTs marked",
                Name, Math.Round(processTime.TotalSeconds, 3), markedEventCount, markedNftCount);
        }
    }
}
