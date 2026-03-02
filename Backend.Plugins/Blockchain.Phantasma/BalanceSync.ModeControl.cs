using System.Collections.Concurrent;
using System.Threading;
using Backend.Commons;
using Backend.PluginEngine;
using Serilog;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    // Enter catch-up mode aggressively when lag grows; exit only when lag is much smaller.
    // This hysteresis prevents mode flapping around the threshold.
    private const int BalanceCatchupEnterLagThreshold = BalanceSyncLagThreshold;
    private const int BalanceCatchupExitLagThreshold = 50;

    // Dirty addresses touched in the last N blocks are treated as "fresh" and get priority.
    private const int BalanceFreshWindowBlocks = 64;

    private sealed class BalanceModeState
    {
        // 1 = catch-up mode, 0 = normal mode.
        public int IsCatchupMode = 1;
        public long LastModeChangedAtUnixSeconds;
    }

    private readonly ConcurrentDictionary<int, BalanceModeState> _balanceModeStates = new();

    // Adaptive drain plan for one balance-sync activation pass.
    // Fresh queue is processed first, backlog second, then fallback.
    private readonly record struct BalanceDrainPlan(
        int FreshBatchSize,
        int FreshBatchLimit,
        int BacklogBatchSize,
        int BacklogBatchLimit,
        int FallbackBatchSize,
        int FallbackBatchLimit);

    private BalanceModeState GetBalanceModeState(int chainId)
    {
        return _balanceModeStates.GetOrAdd(chainId, _ => new BalanceModeState());
    }

    private bool IsBalanceCatchupMode(int chainId)
    {
        var state = GetBalanceModeState(chainId);
        return Volatile.Read(ref state.IsCatchupMode) == 1;
    }

    private bool UpdateBalanceSyncMode(int chainId, string chainName, long lag)
    {
        var state = GetBalanceModeState(chainId);
        var previousIsCatchup = Volatile.Read(ref state.IsCatchupMode) == 1;

        var nextIsCatchup = previousIsCatchup
            ? lag > BalanceCatchupExitLagThreshold
            : lag > BalanceCatchupEnterLagThreshold;

        var nextFlag = nextIsCatchup ? 1 : 0;
        var previousFlag = Interlocked.Exchange(ref state.IsCatchupMode, nextFlag);
        if (previousFlag != nextFlag)
        {
            Interlocked.Exchange(ref state.LastModeChangedAtUnixSeconds, UnixSeconds.Now());

            if (nextIsCatchup)
            {
                Log.Information(
                    "[{Name}][Balances] Entering catch-up mode for {Chain}: lag={Lag}, threshold={Threshold}",
                    Name, chainName, lag, BalanceCatchupEnterLagThreshold);
            }
            else
            {
                Log.Information(
                    "[{Name}][Balances] Entering normal mode for {Chain}: lag={Lag}, threshold={Threshold}. Starting adaptive dirty-drain.",
                    Name, chainName, lag, BalanceCatchupExitLagThreshold);

                // Kick the balance worker immediately on transition to normal mode.
                RequestBalanceSync(chainName);
            }
        }

        return !nextIsCatchup;
    }

    private static long ComputeFreshDirtyCutoff(long chainHeight)
    {
        var cutoff = chainHeight - BalanceFreshWindowBlocks;
        return cutoff > 0 ? cutoff : 1;
    }

    private static BalanceDrainPlan BuildBalanceDrainPlan(int dirtyCount, long lag)
    {
        var freshBatchSize = BalanceDirtyBatchSize;
        var freshBatchLimit = 2;
        var backlogBatchSize = BalanceDirtyBatchSize;
        var backlogBatchLimit = 8;

        // Large backlogs (for example ~39k dirty rows after restore/catch-up)
        // should drain aggressively once we are back to normal mode.
        if (dirtyCount >= 30000)
        {
            freshBatchLimit = 6;
            backlogBatchSize = 700;
            backlogBatchLimit = 70;
        }
        else if (dirtyCount >= 10000)
        {
            freshBatchLimit = 5;
            backlogBatchSize = 500;
            backlogBatchLimit = 50;
        }
        else if (dirtyCount >= 3000)
        {
            freshBatchLimit = 4;
            backlogBatchSize = 350;
            backlogBatchLimit = 32;
        }
        else if (dirtyCount >= 1000)
        {
            freshBatchLimit = 3;
            backlogBatchSize = 250;
            backlogBatchLimit = 20;
        }
        else if (dirtyCount >= 300)
        {
            freshBatchLimit = 3;
            backlogBatchSize = 150;
            backlogBatchLimit = 12;
        }

        // If we are close to tip but still not exactly at zero-lag, keep draining,
        // but reduce backlog pressure to protect block processing throughput.
        if (lag > 0)
        {
            freshBatchLimit = freshBatchLimit > 1 ? freshBatchLimit - 1 : 1;
            backlogBatchLimit = backlogBatchLimit > 6 ? backlogBatchLimit / 2 : backlogBatchLimit;
        }

        return new BalanceDrainPlan(
            FreshBatchSize: freshBatchSize,
            FreshBatchLimit: freshBatchLimit,
            BacklogBatchSize: backlogBatchSize,
            BacklogBatchLimit: backlogBatchLimit,
            FallbackBatchSize: BalanceDirtyBatchSize,
            FallbackBatchLimit: 4);
    }
}
