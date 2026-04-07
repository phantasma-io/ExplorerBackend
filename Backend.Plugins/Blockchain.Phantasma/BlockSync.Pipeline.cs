using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Serilog;
using RpcBlockResult = PhantasmaPhoenix.RPC.Models.BlockResult;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private const int BlockPipelineQueueCapacity = 500;
    private const int BlockPipelineEmptyQueueDelayMs = 100;
    private const int BlockPipelineReliefQueueDelayMs = 500;
    private const int BlockPipelineErrorDelayMs = 1000;
    private const int BlockFetchFailureBackoffBaseMs = 30000;
    private const int BlockFetchFailureBackoffMaxMs = 300000;
    private const string BalanceRefetchTimestampKey = "BALANCE_REFETCH_TIMESTAMP";
    private static readonly SemaphoreSlim BalanceRefetchInitLock = new(1, 1);

    private sealed class BlockPipelineState
    {
        public required string ChainName { get; init; }
        public int ChainId { get; init; }
        // Single-producer/single-consumer bounded queue:
        // fetch pushes RPC blocks, process consumes and writes DB.
        public required Channel<RpcBlockResult> Queue { get; init; }
        public BigInteger NextFetchHeight;
        public int NextFetchInitialized;
        public long EnqueuedBlocks;
        public long ProcessedBlocks;
        public int Started;
        public int InitialRefetchEnsured;
        public BigInteger LastStalledHeight;
        public int ConsecutiveStallCount;
    }

    private readonly ConcurrentDictionary<string, BlockPipelineState> _blockPipelines = new(StringComparer.OrdinalIgnoreCase);

    private BlockPipelineState GetBlockPipelineState(string chainName, int chainId)
    {
        return _blockPipelines.GetOrAdd(chainName, _ => new BlockPipelineState
        {
            ChainName = chainName,
            ChainId = chainId,
            Queue = Channel.CreateBounded<RpcBlockResult>(new BoundedChannelOptions(BlockPipelineQueueCapacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            })
        });
    }

    private void StartBlockPipeline(string chainName)
    {
        int chainId;
        using (MainDbContext databaseContext = new())
        {
            chainId = ChainMethods.GetId(databaseContext, chainName);
        }

        var state = GetBlockPipelineState(chainName, chainId);
        // Startup can be called multiple times by lifecycle code; guard against
        // launching duplicate fetch/process threads for the same chain.
        if (Interlocked.Exchange(ref state.Started, 1) == 1)
            return;

        var fetchThread = new Thread(() =>
            BlockFetchLoopAsync(state).GetAwaiter().GetResult());
        fetchThread.Start();

        var processThread = new Thread(() =>
            BlockProcessLoopAsync(state).GetAwaiter().GetResult());
        processThread.Start();
    }

    private async Task<BlockPipelineSyncSnapshot> ReadPipelineSyncSnapshotAsync(string chainName)
    {
        await using var databaseContext = new MainDbContext();
        var currentHeight = ChainMethods.GetLastProcessedBlock(databaseContext, chainName);
        return new BlockPipelineSyncSnapshot(currentHeight);
    }

    private readonly record struct BlockPipelineSyncSnapshot(BigInteger CurrentHeight);

    private static BigInteger ClampTargetHeight(BigInteger rpcHeight)
    {
        if (Settings.Default.HeightLimit != 0 && rpcHeight >= Settings.Default.HeightLimit)
            return Settings.Default.HeightLimit;

        return rpcHeight;
    }

    private static int RegisterFetchStall(BlockPipelineState state, BigInteger stalledHeight)
    {
        // Track repeated failure on the same next-missing height so we can back off
        // specifically on a stuck window instead of hot-looping the same retry.
        if (state.LastStalledHeight == stalledHeight)
        {
            state.ConsecutiveStallCount++;
        }
        else
        {
            state.LastStalledHeight = stalledHeight;
            state.ConsecutiveStallCount = 1;
        }

        return state.ConsecutiveStallCount;
    }

    private static void ResetFetchStall(BlockPipelineState state)
    {
        state.LastStalledHeight = BigInteger.Zero;
        state.ConsecutiveStallCount = 0;
    }

    private static int ComputeFetchFailureDelayMs(int stallCount)
    {
        var exponent = Math.Min(Math.Max(0, stallCount - 1), 5);
        var delay = BlockFetchFailureBackoffBaseMs * (1 << exponent);
        return Math.Min(BlockFetchFailureBackoffMaxMs, delay);
    }

    private async Task BlockFetchLoopAsync(BlockPipelineState state)
    {
        while (_running)
        {
            try
            {
                if (Interlocked.CompareExchange(ref state.InitialRefetchEnsured, 1, 0) == 0)
                    await EnsureBalanceRefetchInitializedAsync(state.ChainName, state.ChainId);

                var syncSnapshot = await ReadPipelineSyncSnapshotAsync(state.ChainName);
                var currentHeight = syncSnapshot.CurrentHeight;

                height = GetCurrentBlockHeight(state.ChainName);
                Log.Information("[Blocks] Chain height: {Height}, explorer's height {explorerHeight}", height,
                    currentHeight);

                if (currentHeight > height)
                {
                    Log.Warning("[Blocks] RPC is out of sync, RPC: {Height}, explorer: {explorerHeight}", height,
                        currentHeight);
                    var lag = long.MaxValue;
                    UpdateSyncLagState(state.ChainId, lag);
                    UpdateBalanceSyncMode(state.ChainId, state.ChainName, lag);
                    await Task.Delay(Settings.Default.BlocksProcessingInterval * 1000);
                    continue;
                }

                if (Settings.Default.HeightLimit != 0 && currentHeight >= Settings.Default.HeightLimit)
                {
                    Log.Warning("[Blocks] Height limit is reached {Height} >= {HeightLimit}", currentHeight,
                        Settings.Default.HeightLimit);
                    const long lag = 0;
                    UpdateSyncLagState(state.ChainId, lag);
                    var allowBalanceSyncAtHeightLimit = UpdateBalanceSyncMode(state.ChainId, state.ChainName, lag);
                    if (allowBalanceSyncAtHeightLimit)
                        RequestBalanceSync(state.ChainName);
                    await Task.Delay(Settings.Default.BlocksProcessingInterval * 1000);
                    continue;
                }

                var targetHeight = ClampTargetHeight(height);
                var lagBeforeFetch = ComputeLagValue(targetHeight, currentHeight);
                UpdateSyncLagState(state.ChainId, lagBeforeFetch);
                var allowBalanceSync = UpdateBalanceSyncMode(state.ChainId, state.ChainName, lagBeforeFetch);
                var queueDepthBeforeFetch = Math.Max(0,
                    Interlocked.Read(ref state.EnqueuedBlocks) - Interlocked.Read(ref state.ProcessedBlocks));
                var reliefMode = IsRpcReliefMode(state.ChainId);

                if (Interlocked.CompareExchange(ref state.NextFetchInitialized, 1, 0) == 0)
                    state.NextFetchHeight = currentHeight + 1;

                var minimumNextHeight = currentHeight + 1;
                // If DB moved forward faster than our fetch pointer (for example after
                // retry/restart), clamp to the next not-yet-processed height.
                if (state.NextFetchHeight < minimumNextHeight)
                    state.NextFetchHeight = minimumNextHeight;

                if (state.NextFetchHeight > targetHeight)
                {
                    if (allowBalanceSync)
                        RequestBalanceSync(state.ChainName);

                    await Task.Delay(Settings.Default.BlocksProcessingInterval * 1000);
                    continue;
                }

                // In relief mode keep fetch and commit tightly coupled: do not request more
                // blocks while the process stage still has queued work.
                if (reliefMode && queueDepthBeforeFetch > 0)
                {
                    await Task.Delay(BlockPipelineReliefQueueDelayMs);
                    continue;
                }

                var fetchBatchStart = state.NextFetchHeight;
                var fetchBatchSize = reliefMode
                    ? BigInteger.One
                    : BigInteger.Min(FetchBlocksPerIterationMax, targetHeight - fetchBatchStart + 1);
                var fetchConcurrency = reliefMode ? 1 : BlockFetchConcurrencyMax;

                Log.Information(
                    "[{Name}][Blocks][Fetch] Fetching batch of {Count} blocks starting from {StartHeight} for chain {Chain}...",
                    Name,
                    fetchBatchSize,
                    fetchBatchStart,
                    state.ChainName);

                var fetchStopwatch = Stopwatch.StartNew();
                var fetchResult = await GetContiguousBlockPrefix(
                    state.ChainName,
                    fetchBatchStart,
                    fetchBatchSize,
                    fetchConcurrency);
                fetchStopwatch.Stop();
                var blocks = fetchResult.Blocks;

                if (blocks.Count == 0)
                {
                    var stalledHeight = fetchResult.FailedHeight ?? fetchBatchStart;
                    EnterRpcReliefMode(state.ChainId, state.ChainName, $"block {stalledHeight} is not retrievable");
                    var stallCount = RegisterFetchStall(state, stalledHeight);
                    var backoffDelayMs = ComputeFetchFailureDelayMs(stallCount);
                    var requestedRangeEnd = fetchBatchStart + fetchBatchSize - 1;

                    Log.Warning(
                        "[{Name}][Blocks][Fetch] No progress for chain {Chain}. Stalled at block {BlockHeight} while fetching {From}-{To}. Backing off {DelayMs} ms (failure #{FailureCount}).",
                        Name,
                        state.ChainName,
                        stalledHeight,
                        fetchBatchStart,
                        requestedRangeEnd,
                        backoffDelayMs,
                        stallCount);

                    await Task.Delay(backoffDelayMs);
                    continue;
                }

                // Only successful contiguous prefix is allowed to move forward; any gap
                // remains pending so process-stage ordering and CURRENT_HEIGHT semantics stay strict.
                ResetFetchStall(state);

                // BoundedChannel in Wait mode provides backpressure here when process
                // stage is slower than RPC fetch stage.
                foreach (var block in blocks)
                    await state.Queue.Writer.WriteAsync(block);

                state.NextFetchHeight = fetchBatchStart + blocks.Count;
                Interlocked.Add(ref state.EnqueuedBlocks, blocks.Count);

                var fetchSeconds = fetchStopwatch.Elapsed.TotalSeconds;
                var fetchBps = fetchSeconds > 0 ? Math.Round(blocks.Count / fetchSeconds, 2) : 0;
                var queueDepth = Math.Max(0,
                    Interlocked.Read(ref state.EnqueuedBlocks) - Interlocked.Read(ref state.ProcessedBlocks));

                if (fetchResult.UsedFallback)
                {
                    EnterRpcReliefMode(state.ChainId, state.ChainName,
                        $"partial prefix recovered around block {fetchBatchStart}");
                    var resolvedRangeEnd = fetchBatchStart + blocks.Count - 1;
                    Log.Warning(
                        "[{Name}][Blocks][Fetch] Recovered a contiguous prefix for chain {Chain}: {From}-{To} ({Count}/{Requested}) after fallback fetch mode. Next retry starts from {NextHeight}.",
                        Name,
                        state.ChainName,
                        fetchBatchStart,
                        resolvedRangeEnd,
                        blocks.Count,
                        fetchBatchSize,
                        state.NextFetchHeight);
                }

                Log.Information(
                    "[{Name}][Blocks][Fetch] Enqueued {Count} blocks ({From}-{To}) in {FetchTime} sec, {BlocksPerSecond} bps, queue={QueueDepth}",
                    Name,
                    blocks.Count,
                    fetchBatchStart,
                    fetchBatchStart + blocks.Count - 1,
                    Math.Round(fetchSeconds, 3),
                    fetchBps,
                    queueDepth);

                if (allowBalanceSync)
                    RequestBalanceSync(state.ChainName);
            }
            catch (Exception e)
            {
                EnterRpcReliefMode(state.ChainId, state.ChainName, e.Message);
                LogEx.Exception("Block fetch", e);
                await Task.Delay(IsRpcReliefMode(state.ChainId)
                    ? BlockFetchFailureBackoffBaseMs
                    : BlockPipelineErrorDelayMs);
            }
        }
    }

    private async Task BlockProcessLoopAsync(BlockPipelineState state)
    {
        // On processing failure we keep the same block and retry in place to preserve
        // strict in-order semantics and avoid height skips.
        RpcBlockResult retryBlock = null;

        while (_running)
        {
            RpcBlockResult blockToProcess = null;
            try
            {
                if (retryBlock != null)
                {
                    blockToProcess = retryBlock;
                }
                else
                {
                    if (!state.Queue.Reader.TryRead(out blockToProcess))
                    {
                        await Task.Delay(BlockPipelineEmptyQueueDelayMs);
                        continue;
                    }
                }

                await ProcessBlock(blockToProcess, state.ChainName);
                retryBlock = null;

                var processedBlocks = Interlocked.Increment(ref state.ProcessedBlocks);
                var queueDepth = Math.Max(0, Interlocked.Read(ref state.EnqueuedBlocks) - processedBlocks);

                // Process loop also refreshes lag so mode transitions do not wait for the next fetch-cycle sleep.
                if (queueDepth == 0 || processedBlocks % 25 == 0)
                    await RefreshLagFromDbAsync(state);

                if (!IsBalanceCatchupMode(state.ChainId))
                    RequestBalanceSync(state.ChainName);

                RegisterRpcReliefCommitSuccess(state.ChainId, state.ChainName, queueDepth);

                if (queueDepth > 0 && processedBlocks % 100 == 0)
                {
                    Log.Information(
                        "[{Name}][Blocks][Process] Processed queued blocks={Processed}, chain={Chain}, queue={QueueDepth}",
                        Name,
                        processedBlocks,
                        state.ChainName,
                        queueDepth);
                }
            }
            catch (Exception e)
            {
                LogEx.Exception("Block process", e);

                if (blockToProcess != null)
                    retryBlock = blockToProcess;

                await Task.Delay(BlockPipelineErrorDelayMs);
            }
        }
    }

    private async Task EnsureBalanceRefetchInitializedAsync(string chainName, int chainId)
    {
        await BalanceRefetchInitLock.WaitAsync();
        try
        {
            await using var databaseContext = new MainDbContext();
            var marker = await GlobalVariableMethods.GetLongAsync(databaseContext, BalanceRefetchTimestampKey);
            if (marker != 0)
                return;

            Log.Information("[{Name}][Blocks] Marking all addresses for balance refresh", Name);
            await MarkAllBalancesDirtyAsync(databaseContext, chainId);
            await GlobalVariableMethods.UpsertAsync(databaseContext, BalanceRefetchTimestampKey, UnixSeconds.Now(),
                false);
            await databaseContext.SaveChangesAsync();
            Log.Information("[{Name}][Blocks] Finished marking addresses for balance refresh", Name);
        }
        catch (Exception exception)
        {
            Log.Warning("[{Name}][Blocks] Failed to initialize balance refetch marker for chain {Chain}: {Reason}",
                Name, chainName, exception.Message);
        }
        finally
        {
            BalanceRefetchInitLock.Release();
        }
    }

    private async Task RefreshLagFromDbAsync(BlockPipelineState state)
    {
        var rpcTip = height;
        if (rpcTip <= 0)
            return;

        try
        {
            await using var databaseContext = new MainDbContext();
            var currentHeight = ChainMethods.GetLastProcessedBlock(databaseContext, state.ChainName);
            var targetHeight = ClampTargetHeight(rpcTip);
            var lag = ComputeLagValue(targetHeight, currentHeight);
            UpdateSyncLagState(state.ChainId, lag);
            UpdateBalanceSyncMode(state.ChainId, state.ChainName, lag);
        }
        catch (Exception exception)
        {
            Log.Warning("[{Name}][Blocks][Process] Failed to refresh lag from DB for chain {Chain}: {Reason}",
                Name, state.ChainName, exception.Message);
        }
    }
}
