using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin
{
    private const string StakeSnapshotCatchupSource = "balance-sync.catchup.v2";
    private const string StakeSnapshotLegacyCatchupSource = "balance-sync.catchup.v1";
    private const long SecondsPerDay = 86400;

    private static readonly HashSet<string> StakeSnapshotMarketEventKinds = new(StringComparer.Ordinal)
    {
        "OrderCreated",
        "OrderCancelled",
        "OrderFilled",
        "OrderClosed",
        "OrderBid"
    };

    private enum SnapshotStakeClaimType
    {
        Normal = 0,
        MarketEvent = 1,
        SmReward = 2
    }

    private sealed class SnapshotCatchupState
    {
        public readonly Dictionary<string, BigInteger> StakesByAddress = new(StringComparer.Ordinal);
        public BigInteger TotalStakedRaw;
        public BigInteger SoulSupplyRaw;
        public int StakersCount;
        public int MastersCount;
    }

    private sealed record SnapshotAnchorPoint(
        long DateUnixSeconds,
        BigInteger StakedSoulRaw,
        BigInteger SoulSupplyRaw,
        int StakersCount,
        int MastersCount,
        string Source
    );

    private sealed record SnapshotRebuildRange(
        long AnchorDayUnixSeconds,
        long RebuildFromDayUnixSeconds,
        long RebuildToExclusiveDayUnixSeconds,
        string Reason
    );

    private sealed record SnapshotEventRow(
        int EventId,
        int TxId,
        string Kind,
        long TimestampUnixSeconds,
        string PayloadFormat,
        string PayloadJson,
        string PayloadIdentity
    );

    private sealed record SnapshotDailyPoint(
        long DateUnixSeconds,
        string StakedSoulRaw,
        string SoulSupplyRaw,
        int StakersCount,
        int MastersCount,
        decimal StakingRatio,
        long CapturedAtUnixSeconds
    );

    private sealed record SnapshotCatchupResult(int DailyInserted, int MonthlyInserted);

    private static async Task<SnapshotCatchupResult> BackfillMissingStakeSnapshotsAsync(MainDbContext databaseContext,
        Chain chain, long nowUnixSeconds, long currentDayUnixSeconds, long currentMonthUnixSeconds)
    {
        var rebuildRange = await DetectSnapshotRebuildRangeAsync(databaseContext, chain.ID, currentDayUnixSeconds);

        var dailyAffected = 0;
        if (rebuildRange != null)
        {
            dailyAffected = await RebuildDailySnapshotsAsync(databaseContext, chain, rebuildRange, nowUnixSeconds);
        }

        var monthlyAffected = 0;
        if (rebuildRange != null && dailyAffected > 0)
        {
            monthlyAffected += await RebuildMonthlySnapshotsForRebuiltRangeAsync(databaseContext, chain,
                rebuildRange.RebuildFromDayUnixSeconds, rebuildRange.RebuildToExclusiveDayUnixSeconds,
                currentMonthUnixSeconds);
        }

        monthlyAffected += await BackfillMissingMonthlySnapshotsFromDailyAsync(databaseContext, chain,
            currentMonthUnixSeconds);

        return new SnapshotCatchupResult(dailyAffected, monthlyAffected);
    }

    private static async Task<SnapshotRebuildRange> DetectSnapshotRebuildRangeAsync(MainDbContext databaseContext,
        int chainId, long currentDayUnixSeconds)
    {
        // Legacy v1 catch-up rows are known to produce a damaged historical tail.
        // If present, rebuild from the first such day to "today".
        var firstLegacyCatchupDay = await databaseContext.StakingProgressDailies.AsNoTracking()
            .Where(x => x.ChainId == chainId &&
                        x.DATE_UNIX_SECONDS < currentDayUnixSeconds &&
                        x.SOURCE == StakeSnapshotLegacyCatchupSource)
            .OrderBy(x => x.DATE_UNIX_SECONDS)
            .Select(x => (long?)x.DATE_UNIX_SECONDS)
            .FirstOrDefaultAsync();

        if (firstLegacyCatchupDay.HasValue)
        {
            var anchorDay = firstLegacyCatchupDay.Value - SecondsPerDay;
            if (anchorDay < 0)
            {
                Log.Warning(
                    "[{Name}][Balances] Cannot rebuild stake snapshot tail: invalid anchor before {FromDay}",
                    nameof(PhantasmaPlugin), UnixSeconds.Log(firstLegacyCatchupDay.Value));
                return null;
            }

            var anchorExists = await databaseContext.StakingProgressDailies.AsNoTracking()
                .AnyAsync(x => x.ChainId == chainId && x.DATE_UNIX_SECONDS == anchorDay);

            if (!anchorExists)
            {
                Log.Warning(
                    "[{Name}][Balances] Cannot rebuild stake snapshot tail: anchor day {AnchorDay} is missing",
                    nameof(PhantasmaPlugin), UnixSeconds.Log(anchorDay));
                return null;
            }

            return new SnapshotRebuildRange(
                AnchorDayUnixSeconds: anchorDay,
                RebuildFromDayUnixSeconds: firstLegacyCatchupDay.Value,
                RebuildToExclusiveDayUnixSeconds: currentDayUnixSeconds,
                Reason: "legacy-catchup-tail");
        }

        // Normal mode: only fill genuine missing tail days.
        var previousDayUnixSeconds = await databaseContext.StakingProgressDailies.AsNoTracking()
            .Where(x => x.ChainId == chainId && x.DATE_UNIX_SECONDS < currentDayUnixSeconds)
            .Select(x => (long?)x.DATE_UNIX_SECONDS)
            .MaxAsync();

        if (!previousDayUnixSeconds.HasValue)
            return null;

        var missingFromDayUnixSeconds = previousDayUnixSeconds.Value + SecondsPerDay;
        if (missingFromDayUnixSeconds >= currentDayUnixSeconds)
            return null;

        return new SnapshotRebuildRange(
            AnchorDayUnixSeconds: previousDayUnixSeconds.Value,
            RebuildFromDayUnixSeconds: missingFromDayUnixSeconds,
            RebuildToExclusiveDayUnixSeconds: currentDayUnixSeconds,
            Reason: "missing-tail");
    }

    private static async Task<int> RebuildDailySnapshotsAsync(MainDbContext databaseContext, Chain chain,
        SnapshotRebuildRange rebuildRange, long nowUnixSeconds)
    {
        Log.Information(
            "[{Name}][Balances] Rebuilding stake snapshots for {Chain}: {FromDay}..{ToDay} (anchor={AnchorDay}, reason={Reason})",
            nameof(PhantasmaPlugin),
            chain.NAME,
            UnixSeconds.Log(rebuildRange.RebuildFromDayUnixSeconds),
            UnixSeconds.Log(rebuildRange.RebuildToExclusiveDayUnixSeconds - SecondsPerDay),
            UnixSeconds.Log(rebuildRange.AnchorDayUnixSeconds),
            rebuildRange.Reason);

        var anchor = await LoadSnapshotAnchorPointAsync(databaseContext, chain.ID, rebuildRange.AnchorDayUnixSeconds);
        if (anchor == null)
        {
            Log.Warning(
                "[{Name}][Balances] Stake snapshot rebuild skipped for {Chain}: missing anchor row on {AnchorDay}",
                nameof(PhantasmaPlugin), chain.NAME, UnixSeconds.Log(rebuildRange.AnchorDayUnixSeconds));
            return 0;
        }

        var currentState = await LoadCurrentSnapshotStateAsync(databaseContext, chain.ID);
        var eventRows = await LoadSnapshotEventsAsync(chain.NAME, rebuildRange.RebuildFromDayUnixSeconds, nowUnixSeconds);

        try
        {
            ReplaySnapshotEventsReverse(currentState, eventRows);
        }
        catch (Exception exception)
        {
            Log.Warning(
                "[{Name}][Balances] Stake snapshot rebuild skipped for {Chain}: reverse replay failed ({Reason})",
                nameof(PhantasmaPlugin), chain.NAME, exception.Message);
            return 0;
        }

        if (!MatchesSnapshotAnchor(currentState, anchor, out var mismatchReason))
        {
            // Legacy catch-up rows were produced by logic that is already known to be inconsistent
            // around the gen2/gen3 bridge. For this branch we keep replay deltas and re-anchor
            // aggregate counters to the trusted anchor row instead of abandoning the rebuild.
            if (!string.Equals(rebuildRange.Reason, "legacy-catchup-tail", StringComparison.Ordinal) ||
                !TryCalibrateSnapshotStateToAnchor(currentState, anchor, out var calibrationSummary))
            {
                Log.Warning(
                    "[{Name}][Balances] Stake snapshot rebuild skipped for {Chain}: anchor mismatch ({Mismatch})",
                    nameof(PhantasmaPlugin), chain.NAME, mismatchReason);
                return 0;
            }

            Log.Warning(
                "[{Name}][Balances] Stake snapshot rebuild for {Chain} continues with aggregate anchor calibration: {Mismatch}; {Calibration}",
                nameof(PhantasmaPlugin), chain.NAME, mismatchReason, calibrationSummary);
        }

        List<SnapshotDailyPoint> rebuiltSnapshots;
        try
        {
            rebuiltSnapshots = BuildDailySnapshotsForward(currentState, eventRows, rebuildRange.RebuildFromDayUnixSeconds,
                rebuildRange.RebuildToExclusiveDayUnixSeconds);
        }
        catch (Exception exception)
        {
            Log.Warning(
                "[{Name}][Balances] Stake snapshot rebuild skipped for {Chain}: forward replay failed ({Reason})",
                nameof(PhantasmaPlugin), chain.NAME, exception.Message);
            return 0;
        }

        if (rebuiltSnapshots.Count == 0)
            return 0;

        var upserted = await UpsertDailySnapshotsAsync(databaseContext, chain.ID, rebuiltSnapshots);
        if (upserted > 0)
        {
            Log.Information(
                "[{Name}][Balances] Stake snapshot rebuild completed for {Chain}: upserted daily rows={Count}",
                nameof(PhantasmaPlugin), chain.NAME, upserted);
        }

        return upserted;
    }

    private static async Task<SnapshotAnchorPoint> LoadSnapshotAnchorPointAsync(MainDbContext databaseContext,
        int chainId, long anchorDayUnixSeconds)
    {
        var row = await databaseContext.StakingProgressDailies.AsNoTracking()
            .Where(x => x.ChainId == chainId && x.DATE_UNIX_SECONDS == anchorDayUnixSeconds)
            .Select(x => new
            {
                x.DATE_UNIX_SECONDS,
                x.STAKED_SOUL_RAW,
                x.SOUL_SUPPLY_RAW,
                x.STAKERS_COUNT,
                x.MASTERS_COUNT,
                x.SOURCE
            })
            .FirstOrDefaultAsync();

        if (row == null)
            return null;

        if (!BigInteger.TryParse(row.STAKED_SOUL_RAW, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var stakedRaw) ||
            stakedRaw < 0)
        {
            throw new InvalidOperationException(
                $"Stake snapshot anchor contains invalid staked raw on {UnixSeconds.Log(anchorDayUnixSeconds)}");
        }

        if (!BigInteger.TryParse(row.SOUL_SUPPLY_RAW, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var soulSupplyRaw) ||
            soulSupplyRaw <= 0)
        {
            throw new InvalidOperationException(
                $"Stake snapshot anchor contains invalid supply raw on {UnixSeconds.Log(anchorDayUnixSeconds)}");
        }

        return new SnapshotAnchorPoint(
            DateUnixSeconds: row.DATE_UNIX_SECONDS,
            StakedSoulRaw: stakedRaw,
            SoulSupplyRaw: soulSupplyRaw,
            StakersCount: row.STAKERS_COUNT,
            MastersCount: row.MASTERS_COUNT,
            Source: row.SOURCE ?? string.Empty
        );
    }

    private static async Task<SnapshotCatchupState> LoadCurrentSnapshotStateAsync(MainDbContext databaseContext,
        int chainId)
    {
        var state = new SnapshotCatchupState();

        var soulSupplyRawText = await databaseContext.Tokens.AsNoTracking()
            .Where(x => x.ChainId == chainId && x.SYMBOL == "SOUL")
            .Select(x => x.CURRENT_SUPPLY_RAW)
            .FirstOrDefaultAsync();

        if (!BigInteger.TryParse(soulSupplyRawText, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var soulSupplyRaw) ||
            soulSupplyRaw <= 0)
        {
            throw new InvalidOperationException("Cannot load current SOUL supply for stake snapshot catch-up.");
        }

        state.SoulSupplyRaw = soulSupplyRaw;

        var stakeRows = await databaseContext.Addresses.AsNoTracking()
            .Where(x => x.ChainId == chainId &&
                        x.ADDRESS != "NULL" &&
                        !string.IsNullOrEmpty(x.STAKED_AMOUNT_RAW) &&
                        x.STAKED_AMOUNT_RAW != "0")
            .Select(x => new { x.ADDRESS, x.STAKED_AMOUNT_RAW })
            .ToListAsync();

        foreach (var stakeRow in stakeRows)
        {
            if (!BigInteger.TryParse(stakeRow.STAKED_AMOUNT_RAW, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var stakeRaw) ||
                stakeRaw <= 0)
            {
                continue;
            }

            state.StakesByAddress[stakeRow.ADDRESS] = stakeRaw;
            state.TotalStakedRaw += stakeRaw;
            state.StakersCount++;
            if (stakeRaw >= MasterStakeThreshold)
                state.MastersCount++;
        }

        return state;
    }

    private static async Task<List<SnapshotEventRow>> LoadSnapshotEventsAsync(string chainName, long fromTs, long toTs)
    {
        const string sql = """
            SELECT
                e."ID" AS event_id,
                t."ID" AS tx_id,
                ek."NAME" AS kind,
                t."TIMESTAMP_UNIX_SECONDS" AS ts,
                CAST(b."HEIGHT" AS bigint) AS block_height,
                t."INDEX" AS tx_index,
                e."INDEX" AS event_index,
                e."PAYLOAD_FORMAT" AS payload_format,
                e."PAYLOAD_JSON"::text AS payload_json,
                e."RAW_DATA" AS raw_data
            FROM "Events" e
            JOIN "EventKinds" ek ON ek."ID" = e."EventKindId"
            JOIN "Transactions" t ON t."ID" = e."TransactionId"
            JOIN "Blocks" b ON b."ID" = t."BlockId"
            JOIN "Chains" c ON c."ID" = b."ChainId"
            WHERE c."NAME" = @chainName
              AND t."TIMESTAMP_UNIX_SECONDS" >= @fromTs
              AND t."TIMESTAMP_UNIX_SECONDS" <= @toTs
              AND (
                    (
                        ek."NAME" IN ('TokenStake', 'TokenClaim', 'TokenMint', 'TokenBurn')
                        AND e."PAYLOAD_FORMAT" IN ('legacy.backfill.v1', 'live.v1')
                        AND UPPER(COALESCE(e."PAYLOAD_JSON"->'token_event'->>'token', '')) = 'SOUL'
                    )
                    OR
                    (
                        ek."NAME" IN ('OrderCreated', 'OrderCancelled', 'OrderFilled', 'OrderClosed', 'OrderBid')
                        AND e."PAYLOAD_FORMAT" IN ('legacy.backfill.v1', 'live.v1')
                    )
                  )
            ORDER BY
                ts ASC,
                block_height ASC,
                tx_index ASC,
                event_index ASC,
                event_id ASC;
            """;

        var rows = new List<SnapshotEventRow>(1024);

        var connectionString = MainDbContext.GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = 0;
        cmd.Parameters.AddWithValue("chainName", chainName);
        cmd.Parameters.AddWithValue("fromTs", fromTs);
        cmd.Parameters.AddWithValue("toTs", toTs);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var payloadJson = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
            var payloadIdentity = reader.IsDBNull(9)
                ? payloadJson
                : reader.GetString(9);

            rows.Add(new SnapshotEventRow(
                EventId: reader.GetInt32(0),
                TxId: reader.GetInt32(1),
                Kind: reader.GetString(2),
                TimestampUnixSeconds: reader.GetInt64(3),
                PayloadFormat: reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                PayloadJson: payloadJson,
                PayloadIdentity: payloadIdentity));
        }

        return rows;
    }

    private static bool MatchesSnapshotAnchor(SnapshotCatchupState state, SnapshotAnchorPoint anchor,
        out string reason)
    {
        if (state.TotalStakedRaw != anchor.StakedSoulRaw)
        {
            reason =
                $"staked raw mismatch: expected {anchor.StakedSoulRaw}, actual {state.TotalStakedRaw} at {UnixSeconds.Log(anchor.DateUnixSeconds)}";
            return false;
        }

        if (state.SoulSupplyRaw != anchor.SoulSupplyRaw)
        {
            reason =
                $"soul supply mismatch: expected {anchor.SoulSupplyRaw}, actual {state.SoulSupplyRaw} at {UnixSeconds.Log(anchor.DateUnixSeconds)}";
            return false;
        }

        if (state.StakersCount != anchor.StakersCount)
        {
            reason =
                $"stakers mismatch: expected {anchor.StakersCount}, actual {state.StakersCount} at {UnixSeconds.Log(anchor.DateUnixSeconds)}";
            return false;
        }

        if (state.MastersCount != anchor.MastersCount)
        {
            reason =
                $"masters mismatch: expected {anchor.MastersCount}, actual {state.MastersCount} at {UnixSeconds.Log(anchor.DateUnixSeconds)}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool TryCalibrateSnapshotStateToAnchor(SnapshotCatchupState state, SnapshotAnchorPoint anchor,
        out string summary)
    {
        if (anchor.SoulSupplyRaw <= 0 || anchor.StakedSoulRaw < 0 || anchor.StakersCount < 0 || anchor.MastersCount < 0)
        {
            summary = "anchor contains invalid values for calibration";
            return false;
        }

        var stakedDelta = anchor.StakedSoulRaw - state.TotalStakedRaw;
        var supplyDelta = anchor.SoulSupplyRaw - state.SoulSupplyRaw;
        var stakersDelta = anchor.StakersCount - state.StakersCount;
        var mastersDelta = anchor.MastersCount - state.MastersCount;

        state.TotalStakedRaw = anchor.StakedSoulRaw;
        state.SoulSupplyRaw = anchor.SoulSupplyRaw;
        state.StakersCount = anchor.StakersCount;
        state.MastersCount = anchor.MastersCount;

        summary =
            $"delta_staked_raw={stakedDelta}, delta_supply_raw={supplyDelta}, delta_stakers={stakersDelta}, delta_masters={mastersDelta}";
        return true;
    }

    private static void ReplaySnapshotEventsReverse(SnapshotCatchupState state, List<SnapshotEventRow> rows)
    {
        for (var txGroupEnd = rows.Count - 1; txGroupEnd >= 0;)
        {
            var txId = rows[txGroupEnd].TxId;
            var txGroupStart = txGroupEnd;
            while (txGroupStart > 0 && rows[txGroupStart - 1].TxId == txId)
                txGroupStart--;

            var txRows = new List<SnapshotEventRow>(txGroupEnd - txGroupStart + 1);
            for (var i = txGroupStart; i <= txGroupEnd; i++)
                txRows.Add(rows[i]);

            ApplySnapshotTransactionReverse(state, txRows);
            txGroupEnd = txGroupStart - 1;
        }
    }

    private static List<SnapshotDailyPoint> BuildDailySnapshotsForward(SnapshotCatchupState state, List<SnapshotEventRow> rows,
        long rebuildFromDayUnixSeconds, long rebuildToExclusiveDayUnixSeconds)
    {
        var snapshots = new List<SnapshotDailyPoint>(
            (int)((rebuildToExclusiveDayUnixSeconds - rebuildFromDayUnixSeconds) / SecondsPerDay));

        var txGroupStart = 0;
        var dayCursor = rebuildFromDayUnixSeconds;
        while (dayCursor < rebuildToExclusiveDayUnixSeconds)
        {
            var dayEnd = DayEnd(dayCursor);

            while (txGroupStart < rows.Count)
            {
                var txId = rows[txGroupStart].TxId;
                var txGroupEnd = txGroupStart;
                while (txGroupEnd + 1 < rows.Count && rows[txGroupEnd + 1].TxId == txId)
                    txGroupEnd++;

                var txTs = rows[txGroupStart].TimestampUnixSeconds;
                if (txTs > dayEnd)
                    break;

                var txRows = new List<SnapshotEventRow>(txGroupEnd - txGroupStart + 1);
                for (var i = txGroupStart; i <= txGroupEnd; i++)
                    txRows.Add(rows[i]);

                ApplySnapshotTransactionForward(state, txRows);
                txGroupStart = txGroupEnd + 1;
            }

            snapshots.Add(BuildSnapshotDailyPoint(dayCursor, DayEnd(dayCursor), state));
            dayCursor += SecondsPerDay;
        }

        return snapshots;
    }

    private static void ApplySnapshotTransactionReverse(SnapshotCatchupState state, List<SnapshotEventRow> txRows)
    {
        var deduplicatedRows = DeduplicateSnapshotTxRows(txRows);
        var stakeClaimType = ClassifySnapshotStakeClaimType(deduplicatedRows);

        foreach (var row in deduplicatedRows)
        {
            if (row.Kind != "TokenStake" && row.Kind != "TokenClaim" && row.Kind != "TokenMint" &&
                row.Kind != "TokenBurn")
            {
                continue;
            }

            if (!TryParseStructuredTokenPayload(row.PayloadJson, out var tokenSymbol, out var valueRaw, out var address))
                continue;

            if (!string.Equals(tokenSymbol, "SOUL", StringComparison.OrdinalIgnoreCase))
                continue;

            if (valueRaw <= 0)
                continue;

            if (stakeClaimType != SnapshotStakeClaimType.Normal &&
                (row.Kind == "TokenStake" || row.Kind == "TokenClaim"))
            {
                continue;
            }

            switch (row.Kind)
            {
                case "TokenStake":
                    ApplyStakeDeltaStrict(state, address, -valueRaw);
                    break;
                case "TokenClaim":
                    ApplyStakeDeltaStrict(state, address, valueRaw);
                    break;
                case "TokenMint":
                    state.SoulSupplyRaw -= valueRaw;
                    if (state.SoulSupplyRaw < 0)
                    {
                        throw new InvalidOperationException(
                            $"Stake snapshot reverse replay produced negative SOUL supply at event {row.EventId}");
                    }

                    break;
                case "TokenBurn":
                    state.SoulSupplyRaw += valueRaw;
                    break;
            }
        }
    }

    private static void ApplySnapshotTransactionForward(SnapshotCatchupState state, List<SnapshotEventRow> txRows)
    {
        var deduplicatedRows = DeduplicateSnapshotTxRows(txRows);
        var stakeClaimType = ClassifySnapshotStakeClaimType(deduplicatedRows);

        foreach (var row in deduplicatedRows)
        {
            if (row.Kind != "TokenStake" && row.Kind != "TokenClaim" && row.Kind != "TokenMint" &&
                row.Kind != "TokenBurn")
            {
                continue;
            }

            if (!TryParseStructuredTokenPayload(row.PayloadJson, out var tokenSymbol, out var valueRaw, out var address))
                continue;

            if (!string.Equals(tokenSymbol, "SOUL", StringComparison.OrdinalIgnoreCase))
                continue;

            if (valueRaw <= 0)
                continue;

            if (stakeClaimType != SnapshotStakeClaimType.Normal &&
                (row.Kind == "TokenStake" || row.Kind == "TokenClaim"))
            {
                continue;
            }

            switch (row.Kind)
            {
                case "TokenStake":
                    ApplyStakeDeltaStrict(state, address, valueRaw);
                    break;
                case "TokenClaim":
                    ApplyStakeDeltaStrict(state, address, -valueRaw);
                    break;
                case "TokenMint":
                    state.SoulSupplyRaw += valueRaw;
                    break;
                case "TokenBurn":
                    state.SoulSupplyRaw -= valueRaw;
                    if (state.SoulSupplyRaw < 0)
                    {
                        throw new InvalidOperationException(
                            $"Stake snapshot forward replay produced negative SOUL supply at event {row.EventId}");
                    }

                    break;
            }
        }
    }

    private static List<SnapshotEventRow> DeduplicateSnapshotTxRows(List<SnapshotEventRow> txRows)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<SnapshotEventRow>(txRows.Count);

        foreach (var row in txRows)
        {
            var dedupKey = row.Kind + "|" + row.PayloadIdentity;
            if (!seen.Add(dedupKey))
                continue;

            result.Add(row);
        }

        return result;
    }

    private static SnapshotStakeClaimType ClassifySnapshotStakeClaimType(List<SnapshotEventRow> txRows)
    {
        var hasSoulMarketEvent = false;

        foreach (var row in txRows)
        {
            if (row.Kind == "TokenMint" &&
                TryParseStructuredTokenPayload(row.PayloadJson, out var tokenSymbol, out _, out _) &&
                string.Equals(tokenSymbol, "SOUL", StringComparison.OrdinalIgnoreCase))
            {
                return SnapshotStakeClaimType.SmReward;
            }

            if (StakeSnapshotMarketEventKinds.Contains(row.Kind) &&
                TryParseStructuredMarketQuoteSymbol(row.PayloadJson, out var quoteSymbol) &&
                string.Equals(quoteSymbol, "SOUL", StringComparison.OrdinalIgnoreCase))
            {
                hasSoulMarketEvent = true;
            }
        }

        return hasSoulMarketEvent ? SnapshotStakeClaimType.MarketEvent : SnapshotStakeClaimType.Normal;
    }

    private static void ApplyStakeDeltaStrict(SnapshotCatchupState state, string address, BigInteger delta)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new InvalidOperationException("Stake snapshot replay encountered empty address in token payload.");

        state.StakesByAddress.TryGetValue(address, out var oldValue);
        var newValue = oldValue + delta;
        if (newValue < 0)
        {
            throw new InvalidOperationException(
                $"Stake snapshot replay produced negative staked amount for {address}: old={oldValue}, delta={delta}, new={newValue}");
        }

        var wasStaker = oldValue > 0;
        var isStaker = newValue > 0;
        if (wasStaker != isStaker)
            state.StakersCount += isStaker ? 1 : -1;

        var wasMaster = oldValue >= MasterStakeThreshold;
        var isMaster = newValue >= MasterStakeThreshold;
        if (wasMaster != isMaster)
            state.MastersCount += isMaster ? 1 : -1;

        if (newValue == 0)
            state.StakesByAddress.Remove(address);
        else
            state.StakesByAddress[address] = newValue;

        state.TotalStakedRaw += newValue - oldValue;
        if (state.TotalStakedRaw < 0)
        {
            throw new InvalidOperationException(
                $"Stake snapshot replay produced negative total staked value: {state.TotalStakedRaw}");
        }
    }

    private static SnapshotDailyPoint BuildSnapshotDailyPoint(long dateUnixSeconds, long capturedAtUnixSeconds,
        SnapshotCatchupState state)
    {
        decimal stakingRatio = 0;
        if (state.SoulSupplyRaw > 0)
        {
            try
            {
                stakingRatio = (decimal)state.TotalStakedRaw / (decimal)state.SoulSupplyRaw;
            }
            catch (OverflowException)
            {
                stakingRatio = 0;
            }
        }

        return new SnapshotDailyPoint(
            DateUnixSeconds: dateUnixSeconds,
            StakedSoulRaw: state.TotalStakedRaw.ToString(CultureInfo.InvariantCulture),
            SoulSupplyRaw: state.SoulSupplyRaw.ToString(CultureInfo.InvariantCulture),
            StakersCount: state.StakersCount,
            MastersCount: state.MastersCount,
            StakingRatio: stakingRatio,
            CapturedAtUnixSeconds: capturedAtUnixSeconds
        );
    }

    private static async Task<int> UpsertDailySnapshotsAsync(MainDbContext databaseContext, int chainId,
        List<SnapshotDailyPoint> snapshots)
    {
        if (snapshots.Count == 0)
            return 0;

        var fromDay = snapshots.Min(x => x.DateUnixSeconds);
        var toDayExclusive = snapshots.Max(x => x.DateUnixSeconds) + SecondsPerDay;

        var existingRows = await databaseContext.StakingProgressDailies
            .Where(x => x.ChainId == chainId &&
                        x.DATE_UNIX_SECONDS >= fromDay &&
                        x.DATE_UNIX_SECONDS < toDayExclusive)
            .ToDictionaryAsync(x => x.DATE_UNIX_SECONDS);

        var affected = 0;
        foreach (var snapshot in snapshots)
        {
            if (existingRows.TryGetValue(snapshot.DateUnixSeconds, out var row))
            {
                row.STAKED_SOUL_RAW = snapshot.StakedSoulRaw;
                row.SOUL_SUPPLY_RAW = snapshot.SoulSupplyRaw;
                row.STAKERS_COUNT = snapshot.StakersCount;
                row.MASTERS_COUNT = snapshot.MastersCount;
                row.STAKING_RATIO = snapshot.StakingRatio;
                row.CAPTURED_AT_UNIX_SECONDS = snapshot.CapturedAtUnixSeconds;
                row.SOURCE = StakeSnapshotCatchupSource;
            }
            else
            {
                databaseContext.StakingProgressDailies.Add(new StakingProgressDaily
                {
                    ChainId = chainId,
                    DATE_UNIX_SECONDS = snapshot.DateUnixSeconds,
                    STAKED_SOUL_RAW = snapshot.StakedSoulRaw,
                    SOUL_SUPPLY_RAW = snapshot.SoulSupplyRaw,
                    STAKERS_COUNT = snapshot.StakersCount,
                    MASTERS_COUNT = snapshot.MastersCount,
                    STAKING_RATIO = snapshot.StakingRatio,
                    CAPTURED_AT_UNIX_SECONDS = snapshot.CapturedAtUnixSeconds,
                    SOURCE = StakeSnapshotCatchupSource
                });
            }

            affected++;
        }

        if (affected > 0)
            await databaseContext.SaveChangesAsync();

        return affected;
    }

    private static bool TryParseStructuredTokenPayload(string payloadJson, out string tokenSymbol, out BigInteger valueRaw,
        out string address)
    {
        tokenSymbol = string.Empty;
        valueRaw = BigInteger.Zero;
        address = string.Empty;

        if (string.IsNullOrWhiteSpace(payloadJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("token_event", out var tokenEvent) || tokenEvent.ValueKind != JsonValueKind.Object)
                return false;

            if (root.TryGetProperty("address", out var addressElement) && addressElement.ValueKind == JsonValueKind.String)
                address = addressElement.GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(address) &&
                tokenEvent.TryGetProperty("address", out var nestedAddressElement) &&
                nestedAddressElement.ValueKind == JsonValueKind.String)
            {
                address = nestedAddressElement.GetString() ?? string.Empty;
            }

            if (!tokenEvent.TryGetProperty("token", out var tokenElement) || tokenElement.ValueKind != JsonValueKind.String)
                return false;

            tokenSymbol = tokenElement.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tokenSymbol))
                return false;

            if (!tokenEvent.TryGetProperty("value_raw", out var valueElement))
                return false;

            if (valueElement.ValueKind == JsonValueKind.String)
            {
                var valueText = valueElement.GetString();
                return BigInteger.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueRaw);
            }

            if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetInt64(out var valueNumber))
            {
                valueRaw = new BigInteger(valueNumber);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseStructuredMarketQuoteSymbol(string payloadJson, out string quoteSymbol)
    {
        quoteSymbol = string.Empty;

        if (string.IsNullOrWhiteSpace(payloadJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("market_event", out var marketEvent) ||
                marketEvent.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (marketEvent.TryGetProperty("quote_symbol", out var quoteSymbolElement) &&
                quoteSymbolElement.ValueKind == JsonValueKind.String)
            {
                quoteSymbol = quoteSymbolElement.GetString() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(quoteSymbol) &&
                marketEvent.TryGetProperty("quote_token", out var quoteTokenElement) &&
                quoteTokenElement.ValueKind == JsonValueKind.String)
            {
                quoteSymbol = quoteTokenElement.GetString() ?? string.Empty;
            }

            return !string.IsNullOrWhiteSpace(quoteSymbol);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<int> RebuildMonthlySnapshotsForRebuiltRangeAsync(MainDbContext databaseContext,
        Chain chain, long rebuildFromDayUnixSeconds, long rebuildToExclusiveDayUnixSeconds, long currentMonthUnixSeconds)
    {
        var monthCursor = GetMonthStartUnixSeconds(rebuildFromDayUnixSeconds);
        var lastRebuiltDay = rebuildToExclusiveDayUnixSeconds - SecondsPerDay;
        var lastRebuiltMonth = GetMonthStartUnixSeconds(lastRebuiltDay);

        if (monthCursor > lastRebuiltMonth)
            return 0;

        var affected = 0;
        while (monthCursor <= lastRebuiltMonth && monthCursor < currentMonthUnixSeconds)
        {
            var monthEndDayUnixSeconds = GetMonthEndDayUnixSeconds(monthCursor);
            var mastersCount = await databaseContext.StakingProgressDailies.AsNoTracking()
                .Where(x => x.ChainId == chain.ID && x.DATE_UNIX_SECONDS <= monthEndDayUnixSeconds)
                .OrderByDescending(x => x.DATE_UNIX_SECONDS)
                .Select(x => (int?)x.MASTERS_COUNT)
                .FirstOrDefaultAsync();

            if (!mastersCount.HasValue)
            {
                monthCursor = GetNextMonthStartUnixSeconds(monthCursor);
                continue;
            }

            var row = await databaseContext.SoulMastersMonthlies
                .FirstOrDefaultAsync(x => x.ChainId == chain.ID && x.MONTH_UNIX_SECONDS == monthCursor);
            if (row == null)
            {
                row = new SoulMastersMonthly
                {
                    ChainId = chain.ID,
                    MONTH_UNIX_SECONDS = monthCursor
                };
                databaseContext.SoulMastersMonthlies.Add(row);
            }

            row.MASTERS_COUNT = mastersCount.Value;
            row.CAPTURED_AT_UNIX_SECONDS = DayEnd(monthEndDayUnixSeconds);
            row.SOURCE = StakeSnapshotCatchupSource;
            affected++;

            monthCursor = GetNextMonthStartUnixSeconds(monthCursor);
        }

        if (affected > 0)
            await databaseContext.SaveChangesAsync();

        return affected;
    }

    private static async Task<int> BackfillMissingMonthlySnapshotsFromDailyAsync(MainDbContext databaseContext,
        Chain chain, long currentMonthUnixSeconds)
    {
        var previousMonthUnixSeconds = await databaseContext.SoulMastersMonthlies.AsNoTracking()
            .Where(x => x.ChainId == chain.ID && x.MONTH_UNIX_SECONDS < currentMonthUnixSeconds)
            .Select(x => (long?)x.MONTH_UNIX_SECONDS)
            .MaxAsync();

        if (!previousMonthUnixSeconds.HasValue)
            return 0;

        var existingMonths = await databaseContext.SoulMastersMonthlies.AsNoTracking()
            .Where(x => x.ChainId == chain.ID)
            .Select(x => x.MONTH_UNIX_SECONDS)
            .ToHashSetAsync();

        var inserted = 0;
        var monthCursor = GetNextMonthStartUnixSeconds(previousMonthUnixSeconds.Value);
        while (monthCursor < currentMonthUnixSeconds)
        {
            if (existingMonths.Contains(monthCursor))
            {
                monthCursor = GetNextMonthStartUnixSeconds(monthCursor);
                continue;
            }

            var monthEndDayUnixSeconds = GetMonthEndDayUnixSeconds(monthCursor);
            var mastersCount = await databaseContext.StakingProgressDailies.AsNoTracking()
                .Where(x => x.ChainId == chain.ID && x.DATE_UNIX_SECONDS <= monthEndDayUnixSeconds)
                .OrderByDescending(x => x.DATE_UNIX_SECONDS)
                .Select(x => (int?)x.MASTERS_COUNT)
                .FirstOrDefaultAsync();

            if (!mastersCount.HasValue)
            {
                Log.Warning(
                    "[{Name}][Balances] Cannot backfill monthly snapshot for {Chain} month {Month}: no daily data at or before month end",
                    nameof(PhantasmaPlugin), chain.NAME, UnixSeconds.Log(monthCursor));
                monthCursor = GetNextMonthStartUnixSeconds(monthCursor);
                continue;
            }

            databaseContext.SoulMastersMonthlies.Add(new SoulMastersMonthly
            {
                ChainId = chain.ID,
                MONTH_UNIX_SECONDS = monthCursor,
                MASTERS_COUNT = mastersCount.Value,
                CAPTURED_AT_UNIX_SECONDS = DayEnd(monthEndDayUnixSeconds),
                SOURCE = StakeSnapshotCatchupSource
            });
            existingMonths.Add(monthCursor);
            inserted++;

            monthCursor = GetNextMonthStartUnixSeconds(monthCursor);
        }

        if (inserted > 0)
            await databaseContext.SaveChangesAsync();

        return inserted;
    }

    private static long DayStart(long unixSeconds)
    {
        return UnixSeconds.GetDate(unixSeconds);
    }

    private static long DayEnd(long dayStartUnixSeconds)
    {
        return dayStartUnixSeconds + SecondsPerDay - 1;
    }

    private static long GetNextMonthStartUnixSeconds(long monthStartUnixSeconds)
    {
        var monthStart = UnixSeconds.ToDateTime(monthStartUnixSeconds);
        return UnixSeconds.FromDateTime(new DateTime(monthStart.Year, monthStart.Month, 1, 0, 0, 0,
            DateTimeKind.Utc).AddMonths(1));
    }

    private static long GetMonthEndDayUnixSeconds(long monthStartUnixSeconds)
    {
        var monthStart = UnixSeconds.ToDateTime(monthStartUnixSeconds);
        var monthEndDay = new DateTime(monthStart.Year, monthStart.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(1)
            .AddDays(-1);
        return UnixSeconds.FromDateTime(monthEndDay);
    }
}
