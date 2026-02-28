using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.PluginEngine;
using Database.Main;
using Microsoft.EntityFrameworkCore;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private async Task<int> GetDirtyAddressCountAsync(int chainId)
    {
        await using var databaseContext = new MainDbContext();
        return await databaseContext.Addresses
            .Where(x => x.ChainId == chainId && x.BALANCE_DIRTY_BLOCK > 0 && x.ADDRESS != "NULL")
            .CountAsync();
    }

    private async Task<long> GetFreshDirtyCutoffAsync(string chainName)
    {
        await using var databaseContext = new MainDbContext();
        var chain = await ChainMethods.GetAsync(databaseContext, chainName);
        return ComputeFreshDirtyCutoff(chain.CURRENT_HEIGHT);
    }

    private async Task<int> ProcessDirtyBatchWindowAsync(
        string chainName,
        int chainId,
        long? minDirtyInclusive,
        long? maxDirtyExclusive,
        bool newestFirst,
        int batchSize,
        int maxBatches)
    {
        if (batchSize <= 0 || maxBatches <= 0)
            return 0;

        var processed = 0;

        for (var batchIndex = 0; batchIndex < maxBatches && _running; batchIndex++)
        {
            if (IsBalanceCatchupMode(chainId))
                break;

            var processedThisBatch = await ProcessDirtyBatchAsync(
                chainName,
                chainId,
                minDirtyInclusive,
                maxDirtyExclusive,
                newestFirst,
                batchSize);

            if (processedThisBatch == 0)
                break;

            processed += processedThisBatch;

            // If lag started growing again while draining, stop backlog pressure early.
            if (!TryGetRecentLag(chainId, out var lag) || lag > BalanceCatchupExitLagThreshold)
                break;
        }

        return processed;
    }

    private async Task<int> ProcessDirtyBatchAsync(
        string chainName,
        int chainId,
        long? minDirtyInclusive,
        long? maxDirtyExclusive,
        bool newestFirst,
        int batchSize)
    {
        await using var databaseContext = new MainDbContext();
        var chainEntry = await ChainMethods.GetAsync(databaseContext, chainName);

        IQueryable<Address> query = databaseContext.Addresses
            .Where(x => x.ChainId == chainId && x.BALANCE_DIRTY_BLOCK > 0 && x.ADDRESS != "NULL");

        if (minDirtyInclusive.HasValue)
            query = query.Where(x => x.BALANCE_DIRTY_BLOCK >= minDirtyInclusive.Value);

        if (maxDirtyExclusive.HasValue)
            query = query.Where(x => x.BALANCE_DIRTY_BLOCK < maxDirtyExclusive.Value);

        query = newestFirst
            ? query.OrderByDescending(x => x.BALANCE_DIRTY_BLOCK)
            : query.OrderBy(x => x.BALANCE_DIRTY_BLOCK);

        var dirtyAddresses = await query
            .Take(batchSize)
            .ToListAsync();

        if (dirtyAddresses.Count == 0)
            return 0;

        // Snapshot the dirty block marker we observed before RPC/balance writes.
        // Reset step uses this snapshot to avoid clearing rows that were dirtied again
        // concurrently with a newer BALANCE_DIRTY_BLOCK value.
        var dirtySnapshot = dirtyAddresses.ToDictionary(x => x.ID, x => x.BALANCE_DIRTY_BLOCK);
        var addressList = dirtyAddresses.Select(x => x.ADDRESS).ToList();

        await UpdateAddressesBalancesAsync(databaseContext, chainEntry, addressList, 100);
        UpdateStakeMemberships(databaseContext, dirtyAddresses);
        await ResetDirtyBalanceFlagsAsync(databaseContext, dirtySnapshot);
        await databaseContext.SaveChangesAsync();

        return dirtyAddresses.Count;
    }
}
