using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private sealed class BalanceSyncState
    {
        public readonly SemaphoreSlim Signal = new(0, 1);
        public int Pending;
    }

    private const int BalanceSyncLagThreshold = 500;
    private const int BalanceDirtyBatchSize = 100;
    private static readonly BigInteger MasterStakeThreshold = BigInteger.Parse("5000000000000");

    private readonly ConcurrentDictionary<string, BalanceSyncState> _balanceSyncStates = new();

    private BalanceSyncState GetBalanceSyncState(string chainName)
    {
        return _balanceSyncStates.GetOrAdd(chainName, _ => new BalanceSyncState());
    }

    private void StartupBalanceSync(Chain chain)
    {
        var state = GetBalanceSyncState(chain.NAME);
        Thread balanceSyncThread = new(() =>
        {
            BalanceSyncLoopAsync(chain.NAME, chain.ID, state).GetAwaiter().GetResult();
        });
        balanceSyncThread.Start();
    }

    private void RequestBalanceSync(string chainName)
    {
        var state = GetBalanceSyncState(chainName);
        if (Interlocked.Exchange(ref state.Pending, 1) == 0)
        {
            state.Signal.Release();
        }
    }

    private async Task BalanceSyncLoopAsync(string chainName, int chainId, BalanceSyncState state)
    {
        if (Settings.Default.BalanceResyncOnStartup)
        {
            Log.Information("[{Name}][Balances] Marking all addresses for refresh on startup", Name);
            await using var databaseContext = new MainDbContext();
            await MarkAllBalancesDirtyAsync(databaseContext, chainId);
            Log.Information("[{Name}][Balances] Finished marking all addresses for refresh on startup", Name);
        }

        while (_running)
        {
            var signaled = await state.Signal.WaitAsync(TimeSpan.FromSeconds(1));
            if (!_running)
                return;

            if (!signaled)
                continue;

            Interlocked.Exchange(ref state.Pending, 0);

            try
            {
                await ProcessDirtyBalancesAsync(chainName, chainId);
            }
            catch (Exception e)
            {
                LogEx.Exception("Balance sync", e);
            }
        }
    }

    private async Task ProcessDirtyBalancesAsync(string chainName, int chainId)
    {
        var processed = 0;

        while (_running)
        {
            await using var databaseContext = new MainDbContext();
            var chainEntry = await ChainMethods.GetAsync(databaseContext, chainName);

            var dirtyAddresses = await databaseContext.Addresses
                .Where(x => x.ChainId == chainId && x.BALANCE_DIRTY_BLOCK > 0 && x.ADDRESS != "NULL")
                .OrderBy(x => x.BALANCE_DIRTY_BLOCK)
                .Take(BalanceDirtyBatchSize)
                .ToListAsync();

            if (dirtyAddresses.Count == 0)
                break;

            var dirtySnapshot = dirtyAddresses.ToDictionary(x => x.ID, x => x.BALANCE_DIRTY_BLOCK);
            var addressList = dirtyAddresses.Select(x => x.ADDRESS).ToList();

            await UpdateAddressesBalancesAsync(databaseContext, chainEntry, addressList, 100);
            UpdateStakeMemberships(databaseContext, dirtyAddresses);

            foreach (var address in dirtyAddresses)
            {
                if (!dirtySnapshot.TryGetValue(address.ID, out var dirtyBlock))
                    continue;

                await databaseContext.Database.ExecuteSqlRawAsync(
                    "UPDATE \"Addresses\" SET \"BALANCE_DIRTY_BLOCK\" = 0 WHERE \"ID\" = {0} AND \"BALANCE_DIRTY_BLOCK\" = {1}",
                    address.ID, dirtyBlock);
            }

            await databaseContext.SaveChangesAsync();
            processed += dirtyAddresses.Count;
        }

        if (processed > 0)
        {
            try
            {
                await using var snapshotContext = new MainDbContext();
                var chainEntry = await ChainMethods.GetAsync(snapshotContext, chainName);
                await UpsertStakeSnapshotsAsync(snapshotContext, chainEntry, IsHistoricalCatchupAllowed(chainEntry));
            }
            catch (Exception e)
            {
                LogEx.Exception("Stake snapshots sync", e);
            }

            Log.Information("[{Name}][Balances] Updated {Count} addresses for chain {Chain}", Name, processed,
                chainName);
        }
    }

    // Snapshot tables are forward-only: each sync rewrites current day/month rows and never rewinds history.
    private async Task UpsertStakeSnapshotsAsync(MainDbContext databaseContext, Chain chain,
        bool allowHistoricalCatchup)
    {
        var nowUnixSeconds = UnixSeconds.Now();
        var dateUnixSeconds = UnixSeconds.GetDate(nowUnixSeconds);
        var monthUnixSeconds = GetMonthStartUnixSeconds(nowUnixSeconds);

        var soulSupplyRawText = await databaseContext.Tokens
            .Where(x => x.ChainId == chain.ID && x.SYMBOL == "SOUL")
            .Select(x => x.CURRENT_SUPPLY_RAW)
            .FirstOrDefaultAsync();

        if (!BigInteger.TryParse(soulSupplyRawText, out var soulSupplyRaw) || soulSupplyRaw <= 0)
        {
            Log.Warning("[{Name}][Balances] Skipping stake snapshot for {Chain}: missing/invalid SOUL supply raw",
                nameof(PhantasmaPlugin), chain.NAME);
            return;
        }

        var stakedRawValues = await databaseContext.Addresses
            .Where(x => x.ChainId == chain.ID &&
                        x.ADDRESS != "NULL" &&
                        !string.IsNullOrEmpty(x.STAKED_AMOUNT_RAW) &&
                        x.STAKED_AMOUNT_RAW != "0")
            .Select(x => x.STAKED_AMOUNT_RAW)
            .ToListAsync();

        var totalStakedRaw = BigInteger.Zero;
        foreach (var stakedRawValue in stakedRawValues)
        {
            if (BigInteger.TryParse(stakedRawValue, out var parsedStake) && parsedStake > 0)
            {
                totalStakedRaw += parsedStake;
            }
        }

        var stakersCount = await CountOrganizationMembersAsync(databaseContext, chain.ID, "stakers");
        var mastersCount = await CountOrganizationMembersAsync(databaseContext, chain.ID, "masters");

        decimal stakingRatio = 0;
        try
        {
            stakingRatio = (decimal)totalStakedRaw / (decimal)soulSupplyRaw;
        }
        catch (OverflowException)
        {
            // Keep writing raw values even if ratio conversion overflows decimal precision.
            stakingRatio = 0;
        }

        var dailyEntry = await databaseContext.StakingProgressDailies
            .FirstOrDefaultAsync(x => x.ChainId == chain.ID && x.DATE_UNIX_SECONDS == dateUnixSeconds);

        if (dailyEntry == null)
        {
            dailyEntry = new StakingProgressDaily
            {
                ChainId = chain.ID,
                DATE_UNIX_SECONDS = dateUnixSeconds,
                SOURCE = "balance-sync.v1"
            };
            databaseContext.StakingProgressDailies.Add(dailyEntry);
        }

        dailyEntry.STAKED_SOUL_RAW = totalStakedRaw.ToString();
        dailyEntry.SOUL_SUPPLY_RAW = soulSupplyRaw.ToString();
        dailyEntry.STAKERS_COUNT = stakersCount;
        dailyEntry.MASTERS_COUNT = mastersCount;
        dailyEntry.STAKING_RATIO = stakingRatio;
        dailyEntry.CAPTURED_AT_UNIX_SECONDS = nowUnixSeconds;

        var monthlyEntry = await databaseContext.SoulMastersMonthlies
            .FirstOrDefaultAsync(x => x.ChainId == chain.ID && x.MONTH_UNIX_SECONDS == monthUnixSeconds);

        if (monthlyEntry == null)
        {
            monthlyEntry = new SoulMastersMonthly
            {
                ChainId = chain.ID,
                MONTH_UNIX_SECONDS = monthUnixSeconds,
                SOURCE = "balance-sync.v1"
            };
            databaseContext.SoulMastersMonthlies.Add(monthlyEntry);
        }

        monthlyEntry.MASTERS_COUNT = mastersCount;
        monthlyEntry.CAPTURED_AT_UNIX_SECONDS = nowUnixSeconds;

        await databaseContext.SaveChangesAsync();

        if (!allowHistoricalCatchup)
            return;

        var catchupResult = await BackfillMissingStakeSnapshotsAsync(databaseContext, chain, nowUnixSeconds,
            dateUnixSeconds, monthUnixSeconds);
        if (catchupResult.DailyInserted > 0 || catchupResult.MonthlyInserted > 0)
        {
            Log.Information(
                "[{Name}][Balances] Stake snapshots catch-up inserted daily={DailyInserted}, monthly={MonthlyInserted} for chain {Chain}",
                Name, catchupResult.DailyInserted, catchupResult.MonthlyInserted, chain.NAME);
        }
    }

    private bool IsHistoricalCatchupAllowed(Chain chain)
    {
        if (!BigInteger.TryParse(chain.CURRENT_HEIGHT, out var currentHeight))
            return false;

        // Align tip detection with block sync semantics: if heightLimit is active,
        // reaching that limit is considered "at tip" for background jobs.
        if (Settings.Default.HeightLimit != 0 && currentHeight >= Settings.Default.HeightLimit)
            return true;

        // The block loop computes lag and only requests balance sync when lag <= threshold.
        // Reuse the same gate here to avoid races against a moving RPC tip.
        if (IsBackgroundSyncAllowed(chain.ID))
            return true;

        try
        {
            var rpcHeight = GetCurrentBlockHeight(chain.NAME);
            var targetHeight = rpcHeight;
            if (Settings.Default.HeightLimit != 0 && targetHeight >= Settings.Default.HeightLimit)
                targetHeight = Settings.Default.HeightLimit;

            return currentHeight >= targetHeight;
        }
        catch (Exception exception)
        {
            Log.Warning("[{Name}][Balances] Unable to validate tip for {Chain}: {Reason}", Name, chain.NAME,
                exception.Message);
            return false;
        }
    }

    private static async Task<int> CountOrganizationMembersAsync(MainDbContext databaseContext, int chainId,
        string organizationName)
    {
        var organizationId = await databaseContext.Organizations
            .Where(x => x.NAME == organizationName)
            .Select(x => (int?)x.ID)
            .FirstOrDefaultAsync();

        if (!organizationId.HasValue)
            return 0;

        return await databaseContext.OrganizationAddresses
            .Where(x => x.OrganizationId == organizationId.Value && x.Address.ChainId == chainId)
            .CountAsync();
    }

    private static long GetMonthStartUnixSeconds(long unixSeconds)
    {
        var date = UnixSeconds.ToDateTime(unixSeconds);
        return UnixSeconds.FromDateTime(new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    private static void UpdateStakeMemberships(MainDbContext databaseContext, List<Address> addresses)
    {
        var stakersOrg = OrganizationMethods.Get(databaseContext, "stakers");
        var mastersOrg = OrganizationMethods.Get(databaseContext, "masters");

        if (stakersOrg == null && mastersOrg == null)
            return;

        foreach (var address in addresses)
        {
            var stakedAmount = BigInteger.Zero;
            if (!string.IsNullOrWhiteSpace(address.STAKED_AMOUNT_RAW))
                BigInteger.TryParse(address.STAKED_AMOUNT_RAW, out stakedAmount);

            var isStaker = stakedAmount > 0;
            var isMaster = stakedAmount >= MasterStakeThreshold;

            if (stakersOrg != null)
                OrganizationAddressMethods.SetMembership(databaseContext, stakersOrg, address, isStaker);

            if (mastersOrg != null)
                OrganizationAddressMethods.SetMembership(databaseContext, mastersOrg, address, isMaster);
        }
    }
}
