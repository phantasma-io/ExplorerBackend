#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using DbRejectedTransactionCandidate = Database.Main.RejectedTransactionCandidate;

namespace Backend.Service.Api;

public static class GetRejectedTransactions
{
    private const string StatusNotInBlockTxs = "not_in_block_txs";
    private const string StatusBlockUnavailable = "block_unavailable";

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class RpcTransaction
    {
        public string? Hash { get; set; }
        public long? Timestamp { get; set; }
        public long? BlockHeight { get; set; }
        public string? BlockHash { get; set; }
        public string? Script { get; set; }
        public string? Payload { get; set; }
        public string? Result { get; set; }
        public string? DebugComment { get; set; }
        public string? Fee { get; set; }
        public long? Expiration { get; set; }
        public string? GasPrice { get; set; }
        public string? GasLimit { get; set; }
        public string? State { get; set; }
        public string? Sender { get; set; }
        public string? GasPayer { get; set; }
        public string? GasTarget { get; set; }
    }

    private sealed class RpcBlock
    {
        public string? Hash { get; set; }
        public RpcBlockTransaction[]? Txs { get; set; }
    }

    private sealed class RpcBlockTransaction
    {
        public string? Hash { get; set; }
    }

    private sealed class RpcResponse<T>
    {
        public T? Payload { get; init; }
        public string? RawJson { get; init; }
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ExplorerBackend");
        return httpClient;
    }

    [ProducesResponseType(typeof(RejectedTransactionCandidateResult), (int)HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(RejectedTransactionCandidateResult),
        "Returns rejected transaction candidates captured from recent RPC diagnostics.", cacheDuration: 0,
        cacheTag: "rejected-transactions")]
    public static async Task<RejectedTransactionCandidateResult> Execute(
        // ReSharper disable InconsistentNaming
        string hash = "",
        string chain = "",
        int capture = 1
    // ReSharper enable InconsistentNaming
    )
    {
        if (string.IsNullOrWhiteSpace(hash) || !ArgValidation.CheckHash(hash.ToUpperInvariant()))
            throw new ApiParameterException("Unsupported value for 'hash' parameter.");

        if (!string.IsNullOrWhiteSpace(chain) && !ArgValidation.CheckChain(chain))
            throw new ApiParameterException("Unsupported value for 'chain' parameter.");

        var settings = Settings.Default.RejectedTransactionCandidates;
        var hashUpper = hash.ToUpperInvariant();
        var nexus = string.IsNullOrWhiteSpace(settings.Nexus) ? "unknown" : settings.Nexus.Trim().ToLowerInvariant();
        var defaultChain = string.IsNullOrWhiteSpace(settings.DefaultChain) ? "main" : settings.DefaultChain;
        var chainName = string.IsNullOrWhiteSpace(chain)
            ? defaultChain.Trim().ToLowerInvariant()
            : chain.Trim().ToLowerInvariant();

        await using MainDbContext databaseContext = new();

        if (await databaseContext.Transactions.AsNoTracking()
                .AnyAsync(x => x.HASH == hashUpper && x.Block.Chain.NAME == chainName))
            return Empty();

        var existing = await Query(databaseContext, hashUpper, nexus, chainName);
        if (existing.Length > 0)
            return Result(existing);

        if (capture != 1 || !settings.CaptureEnabled)
            return Empty();

        var captured = await TryCaptureAsync(databaseContext, hashUpper, nexus, chainName, settings);
        if (captured == null)
            return Empty();

        await databaseContext.SaveChangesAsync();

        return Result([captured]);
    }

    private static async Task<DbRejectedTransactionCandidate[]> Query(MainDbContext databaseContext, string hash,
        string nexus, string chain)
    {
        return await databaseContext.RejectedTransactionCandidates
            .AsNoTracking()
            .Where(x => x.HASH == hash && x.NEXUS == nexus && x.CHAIN == chain)
            .OrderByDescending(x => x.LAST_SEEN_AT_UNIX_SECONDS)
            .ToArrayAsync();
    }

    private static async Task<DbRejectedTransactionCandidate?> TryCaptureAsync(MainDbContext databaseContext,
        string hash, string nexus, string chain, Settings.RejectedTransactionCandidateSettings settings)
    {
        var rest = settings.GetRest();
        if (string.IsNullOrWhiteSpace(rest))
            return null;

        var txUrl = $"{rest}/api/v1/getTransaction?hashText={Uri.EscapeDataString(hash)}";
        var txResponse = await TryFetchJsonAsync<RpcTransaction>(txUrl, settings.RpcTimeoutSeconds);
        var rpcTx = txResponse?.Payload;
        if (rpcTx == null || string.IsNullOrWhiteSpace(rpcTx.Hash))
            return null;

        if (!string.Equals(rpcTx.Hash, hash, StringComparison.OrdinalIgnoreCase))
            return null;

        var blockHash = rpcTx.BlockHash ?? string.Empty;
        string? blockResponseJson = null;
        var canonicalStatus = StatusBlockUnavailable;

        if (rpcTx.BlockHeight.HasValue && rpcTx.BlockHeight.Value >= 0)
        {
            var blockUrl =
                $"{rest}/api/v1/getBlockByHeight?chainInput={Uri.EscapeDataString(chain)}&height={rpcTx.BlockHeight.Value.ToString(CultureInfo.InvariantCulture)}";
            var blockResponse = await TryFetchJsonAsync<RpcBlock>(blockUrl, settings.RpcTimeoutSeconds);
            var rpcBlock = blockResponse?.Payload;
            blockResponseJson = blockResponse?.RawJson;

            if (rpcBlock?.Txs != null)
            {
                blockHash = rpcBlock.Hash ?? string.Empty;
                if (rpcBlock.Txs.Any(x => string.Equals(x.Hash, hash, StringComparison.OrdinalIgnoreCase)))
                {
                    Log.Information(
                        "[RejectedTxCapture] RPC tx {Hash} is present in canonical block.txs for {Nexus}/{Chain}; not saving as rejected candidate.",
                        hash, nexus, chain);
                    return null;
                }

                canonicalStatus = StatusNotInBlockTxs;
            }
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var candidate = new DbRejectedTransactionCandidate
        {
            HASH = hash,
            NEXUS = nexus,
            CHAIN = chain,
            BLOCK_HEIGHT = rpcTx.BlockHeight,
            BLOCK_HASH = blockHash,
            TIMESTAMP_UNIX_SECONDS = rpcTx.Timestamp,
            STATE = rpcTx.State,
            RESULT = rpcTx.Result,
            DEBUG_COMMENT = rpcTx.DebugComment,
            PAYLOAD = rpcTx.Payload,
            SCRIPT_RAW = rpcTx.Script,
            FEE_RAW = rpcTx.Fee,
            EXPIRATION = rpcTx.Expiration,
            GAS_PRICE_RAW = rpcTx.GasPrice,
            GAS_LIMIT_RAW = rpcTx.GasLimit,
            SENDER = rpcTx.Sender,
            GAS_PAYER = rpcTx.GasPayer,
            GAS_TARGET = rpcTx.GasTarget,
            CANONICAL_STATUS = canonicalStatus,
            RPC_RESPONSE_JSON = txResponse?.RawJson,
            BLOCK_RESPONSE_JSON = blockResponseJson,
            CAPTURED_AT_UNIX_SECONDS = now,
            UPDATED_AT_UNIX_SECONDS = now,
            LAST_SEEN_AT_UNIX_SECONDS = now
        };

        Log.Information(
            "[RejectedTxCapture] Captured rejected tx candidate {Hash} ({Nexus}/{Chain}, block {BlockHeight}, status {Status})",
            hash, nexus, chain, candidate.BLOCK_HEIGHT, canonicalStatus);

        return await RejectedTransactionCandidateMethods.UpsertAsync(databaseContext, candidate);
    }

    private static async Task<RpcResponse<T>?> TryFetchJsonAsync<T>(string url, int timeoutSeconds)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds > 0 ? timeoutSeconds : 8));
            using var response = await HttpClient.GetAsync(url, cts.Token);
            var raw = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(raw))
                return null;

            using var document = JsonDocument.Parse(raw);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out _))
            {
                return null;
            }

            var payload = JsonSerializer.Deserialize<T>(raw, JsonOptions);
            return new RpcResponse<T>
            {
                Payload = payload,
                RawJson = raw
            };
        }
        catch (Exception e)
        {
            Log.Warning(e, "[RejectedTxCapture] Failed to fetch {Url}", url);
            return null;
        }
    }

    private static RejectedTransactionCandidateResult Empty()
    {
        return new RejectedTransactionCandidateResult
        {
            rejected_transactions = Array.Empty<RejectedTransactionCandidate>()
        };
    }

    private static RejectedTransactionCandidateResult Result(DbRejectedTransactionCandidate[] candidates)
    {
        return new RejectedTransactionCandidateResult
        {
            rejected_transactions = candidates.Select(ToApi).ToArray()
        };
    }

    private static RejectedTransactionCandidate ToApi(DbRejectedTransactionCandidate candidate)
    {
        return new RejectedTransactionCandidate
        {
            hash = candidate.HASH,
            nexus = candidate.NEXUS,
            chain = candidate.CHAIN,
            block_height = candidate.BLOCK_HEIGHT?.ToString(CultureInfo.InvariantCulture),
            block_hash = string.IsNullOrWhiteSpace(candidate.BLOCK_HASH) ? null : candidate.BLOCK_HASH,
            date = candidate.TIMESTAMP_UNIX_SECONDS?.ToString(CultureInfo.InvariantCulture),
            state = candidate.STATE,
            result = candidate.RESULT,
            debug_comment = candidate.DEBUG_COMMENT,
            payload = candidate.PAYLOAD,
            script_raw = candidate.SCRIPT_RAW,
            fee_raw = candidate.FEE_RAW,
            expiration = candidate.EXPIRATION?.ToString(CultureInfo.InvariantCulture),
            gas_price_raw = candidate.GAS_PRICE_RAW,
            gas_limit_raw = candidate.GAS_LIMIT_RAW,
            sender = candidate.SENDER,
            gas_payer = candidate.GAS_PAYER,
            gas_target = candidate.GAS_TARGET,
            canonical_status = candidate.CANONICAL_STATUS,
            captured_at = candidate.CAPTURED_AT_UNIX_SECONDS.ToString(CultureInfo.InvariantCulture),
            updated_at = candidate.UPDATED_AT_UNIX_SECONDS.ToString(CultureInfo.InvariantCulture),
            rpc_response_json = candidate.RPC_RESPONSE_JSON,
            block_response_json = candidate.BLOCK_RESPONSE_JSON
        };
    }
}
