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
            Log.Information("[{Name}][Balances] Updated {Count} addresses for chain {Chain}", Name, processed,
                chainName);
        }
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
