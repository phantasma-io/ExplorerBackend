using System;
#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Microsoft.EntityFrameworkCore;
using PhantasmaPhoenix.RPC.Models;
using Serilog;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private const int FailedTxDebugCommentRetryIntervalSeconds = 3;
    private const int FailedTxDebugCommentRpcTimeoutSeconds = 10;
    private const int FailedTxDebugCommentMaxChecks = 12;
    private const int FailedTxDebugCommentStartupSeedWindowSeconds = 7 * 24 * 60 * 60;
    private const int FailedTxDebugCommentStartupSeedBatchSize = 100;

    private sealed class FailedTxDebugCommentState
    {
        public readonly SemaphoreSlim Signal = new(0, 1);
        public int Pending;
        public readonly ConcurrentDictionary<string, FailedTxDebugCommentPending> PendingTransactions =
            new(StringComparer.Ordinal);
    }

    private sealed class FailedTxDebugCommentPending
    {
        public required string Hash { get; init; }
        public int CheckCount { get; init; }
        public long NextAttemptAtUnixSeconds { get; init; }
    }

    private readonly ConcurrentDictionary<string, FailedTxDebugCommentState> _failedTxDebugCommentStates =
        new(StringComparer.OrdinalIgnoreCase);

    private FailedTxDebugCommentState GetFailedTxDebugCommentState(string chainName)
    {
        return _failedTxDebugCommentStates.GetOrAdd(chainName, _ => new FailedTxDebugCommentState());
    }

    private static bool IsFailedTransactionState(string? stateName)
    {
        return string.Equals(stateName, "Break", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(stateName, "Fault", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldEnqueueDebugCommentRecovery(string? stateName, string? debugComment)
    {
        return IsFailedTransactionState(stateName) && string.IsNullOrWhiteSpace(debugComment);
    }

    private void StartupFailedTransactionDebugCommentSync(Chain chain)
    {
        var state = GetFailedTxDebugCommentState(chain.NAME);
        Thread failedTxDebugCommentThread = new(() =>
        {
            FailedTxDebugCommentLoopAsync(chain.NAME, chain.ID, state).GetAwaiter().GetResult();
        });
        failedTxDebugCommentThread.Start();

        SeedRecentFailedTransactionsMissingDebugCommentAsync(chain.NAME, chain.ID).GetAwaiter().GetResult();
    }

    private void RequestFailedTransactionDebugCommentSync(string chainName, string txHash,
        long? nextAttemptAtUnixSeconds = null, int checkCount = 0)
    {
        if (string.IsNullOrWhiteSpace(chainName) || string.IsNullOrWhiteSpace(txHash))
            return;

        var state = GetFailedTxDebugCommentState(chainName);
        var now = UnixSeconds.Now();
        var targetAttemptAt = nextAttemptAtUnixSeconds ?? now;

        state.PendingTransactions.AddOrUpdate(
            txHash,
            _ => new FailedTxDebugCommentPending
            {
                Hash = txHash,
                CheckCount = checkCount,
                NextAttemptAtUnixSeconds = targetAttemptAt
            },
            (_, existing) =>
            {
                var mergedCheckCount = Math.Max(existing.CheckCount, checkCount);
                var mergedAttemptAt = Math.Min(existing.NextAttemptAtUnixSeconds, targetAttemptAt);
                return new FailedTxDebugCommentPending
                {
                    Hash = txHash,
                    CheckCount = mergedCheckCount,
                    NextAttemptAtUnixSeconds = mergedAttemptAt
                };
            });

        if (Interlocked.Exchange(ref state.Pending, 1) == 0)
            state.Signal.Release();
    }

    private async Task FailedTxDebugCommentLoopAsync(string chainName, int chainId,
        FailedTxDebugCommentState state)
    {
        while (_running)
        {
            var signaled = await state.Signal.WaitAsync(TimeSpan.FromSeconds(1));
            if (!_running)
                return;

            if (!signaled && !HasDueFailedTxDebugCommentWork(state))
                continue;

            Interlocked.Exchange(ref state.Pending, 0);

            try
            {
                await ProcessFailedTxDebugCommentAsync(chainName, chainId, state);
            }
            catch (Exception e)
            {
                LogEx.Exception("Failed tx debug comment sync", e);
            }
        }
    }

    private static bool HasDueFailedTxDebugCommentWork(FailedTxDebugCommentState state)
    {
        var now = UnixSeconds.Now();
        return state.PendingTransactions.Values.Any(x => x.NextAttemptAtUnixSeconds <= now);
    }

    private async Task SeedRecentFailedTransactionsMissingDebugCommentAsync(string chainName, int chainId)
    {
        try
        {
            var now = UnixSeconds.Now();
            var cutoffUnixSeconds = now - FailedTxDebugCommentStartupSeedWindowSeconds;

            await using var databaseContext = new MainDbContext();
            var failedStateIds = await databaseContext.TransactionStates
                .Where(x => x.NAME == "Break" || x.NAME == "Fault")
                .Select(x => x.ID)
                .ToArrayAsync();

            if (failedStateIds.Length == 0)
                return;

            var txHashes = await databaseContext.Transactions
                .Where(x => x.Block.ChainId == chainId &&
                            failedStateIds.Contains(x.StateId) &&
                            x.TIMESTAMP_UNIX_SECONDS >= cutoffUnixSeconds &&
                            (x.DEBUG_COMMENT == null || x.DEBUG_COMMENT == ""))
                .OrderByDescending(x => x.TIMESTAMP_UNIX_SECONDS)
                .Select(x => x.HASH)
                .Take(FailedTxDebugCommentStartupSeedBatchSize)
                .ToListAsync();

            foreach (var txHash in txHashes)
                RequestFailedTransactionDebugCommentSync(chainName, txHash);

            if (txHashes.Count > 0)
            {
                Log.Information(
                    "[{Name}][FailedTxDebug] Seeded {Count} failed transactions without debug comment for chain {Chain}",
                    Name,
                    txHashes.Count,
                    chainName);
            }
        }
        catch (Exception e)
        {
            LogEx.Exception("Failed tx debug comment startup seed", e);
        }
    }

    private async Task ProcessFailedTxDebugCommentAsync(string chainName, int chainId,
        FailedTxDebugCommentState state)
    {
        if (!TryGetRecentLag(chainId, out var lag))
            return;

        // Debug comment enrichment is best-effort. Keep it off the hot path until block sync is healthy.
        if (lag != 0 || IsRpcReliefMode(chainId))
            return;

        var now = UnixSeconds.Now();
        var dueItem = state.PendingTransactions.Values
            .Where(x => x.NextAttemptAtUnixSeconds <= now)
            .OrderBy(x => x.NextAttemptAtUnixSeconds)
            .ThenBy(x => x.CheckCount)
            .FirstOrDefault();

        if (dueItem == null)
            return;

        var rpcTransaction = await TryGetTransactionFromRpcAsync(dueItem.Hash);
        if (!state.PendingTransactions.TryGetValue(dueItem.Hash, out var currentItem))
            return;

        if (rpcTransaction != null && !string.IsNullOrWhiteSpace(rpcTransaction.DebugComment))
        {
            await PersistFailedTransactionDebugCommentAsync(dueItem.Hash, rpcTransaction.Result, rpcTransaction.DebugComment);
            state.PendingTransactions.TryRemove(dueItem.Hash, out _);
            return;
        }

        var nextCheckCount = currentItem.CheckCount + 1;
        if (nextCheckCount >= FailedTxDebugCommentMaxChecks)
        {
            state.PendingTransactions.TryRemove(dueItem.Hash, out _);
            Log.Debug(
                "[{Name}][FailedTxDebug] Stopped waiting for debug comment on tx {TxHash} after {Checks} checks",
                Name,
                dueItem.Hash,
                nextCheckCount);
            return;
        }

        state.PendingTransactions[dueItem.Hash] = new FailedTxDebugCommentPending
        {
            Hash = dueItem.Hash,
            CheckCount = nextCheckCount,
            NextAttemptAtUnixSeconds = now + FailedTxDebugCommentRetryIntervalSeconds
        };
    }

    private async Task<TransactionResult?> TryGetTransactionFromRpcAsync(string txHash)
    {
        try
        {
            var encodedHash = Uri.EscapeDataString(txHash);
            var url = $"{Settings.Default.GetRest()}/api/v1/getTransaction?hashText={encodedHash}";
            var (transaction, _) = await Client.ApiRequestAsync<TransactionResult>(url,
                FailedTxDebugCommentRpcTimeoutSeconds);
            return transaction;
        }
        catch (Exception e)
        {
            Log.Warning(
                "[{Name}][FailedTxDebug] Failed to fetch tx {TxHash} while waiting for debug comment: {Reason}",
                Name,
                txHash,
                e.Message);
            return null;
        }
    }

    private static async Task PersistFailedTransactionDebugCommentAsync(string txHash, string? result,
        string debugComment)
    {
        if (string.IsNullOrWhiteSpace(debugComment))
            return;

        await using var databaseContext = new MainDbContext();
        var transaction = await databaseContext.Transactions
            .FirstOrDefaultAsync(x => x.HASH == txHash);

        if (transaction == null)
            return;

        var changed = false;
        if (!string.Equals(transaction.DEBUG_COMMENT, debugComment, StringComparison.Ordinal))
        {
            transaction.DEBUG_COMMENT = debugComment;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(transaction.RESULT) && !string.IsNullOrWhiteSpace(result))
        {
            transaction.RESULT = result;
            changed = true;
        }

        if (changed)
            await databaseContext.SaveChangesAsync();
    }
}
