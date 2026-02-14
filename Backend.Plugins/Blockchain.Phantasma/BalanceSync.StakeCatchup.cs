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
    private const string StakeSnapshotCatchupSource = "balance-sync.catchup.v1";
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
        public readonly HashSet<string> Stakers = new(StringComparer.Ordinal);
        public readonly HashSet<string> Masters = new(StringComparer.Ordinal);
        public BigInteger TotalStakedRaw;
        public BigInteger SoulSupplyRaw;
    }

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
        var previousDayUnixSeconds = await databaseContext.StakingProgressDailies.AsNoTracking()
            .Where(x => x.ChainId == chain.ID && x.DATE_UNIX_SECONDS < currentDayUnixSeconds)
            .Select(x => (long?)x.DATE_UNIX_SECONDS)
            .MaxAsync();

        var dailyInserted = 0;
        if (previousDayUnixSeconds.HasValue)
        {
            var missingFromDayUnixSeconds = previousDayUnixSeconds.Value + SecondsPerDay;
            if (missingFromDayUnixSeconds < currentDayUnixSeconds)
            {
                dailyInserted = await BackfillMissingDailySnapshotsReverseAsync(databaseContext, chain,
                    nowUnixSeconds, currentDayUnixSeconds, missingFromDayUnixSeconds);
            }
        }

        var monthlyInserted = await BackfillMissingMonthlySnapshotsFromDailyAsync(databaseContext, chain,
            currentMonthUnixSeconds);

        return new SnapshotCatchupResult(dailyInserted, monthlyInserted);
    }

    private static async Task<int> BackfillMissingDailySnapshotsReverseAsync(MainDbContext databaseContext, Chain chain,
        long nowUnixSeconds, long currentDayUnixSeconds, long missingFromDayUnixSeconds)
    {
        Log.Information(
            "[{Name}][Balances] Starting stake snapshot catch-up for {Chain}: missing daily range {FromDay}..{ToDay}",
            nameof(PhantasmaPlugin), chain.NAME, UnixSeconds.Log(missingFromDayUnixSeconds),
            UnixSeconds.Log(currentDayUnixSeconds - SecondsPerDay));

        var state = await LoadCurrentSnapshotStateAsync(databaseContext, chain.ID);
        var snapshots = await BuildMissingDailySnapshotsReverseAsync(chain.NAME, state, nowUnixSeconds,
            currentDayUnixSeconds, missingFromDayUnixSeconds);

        if (snapshots.Count == 0)
            return 0;

        var existingDays = await databaseContext.StakingProgressDailies.AsNoTracking()
            .Where(x => x.ChainId == chain.ID
                        && x.DATE_UNIX_SECONDS >= missingFromDayUnixSeconds
                        && x.DATE_UNIX_SECONDS < currentDayUnixSeconds)
            .Select(x => x.DATE_UNIX_SECONDS)
            .ToHashSetAsync();

        var inserted = 0;
        foreach (var snapshot in snapshots)
        {
            if (existingDays.Contains(snapshot.DateUnixSeconds))
                continue;

            databaseContext.StakingProgressDailies.Add(new StakingProgressDaily
            {
                ChainId = chain.ID,
                DATE_UNIX_SECONDS = snapshot.DateUnixSeconds,
                STAKED_SOUL_RAW = snapshot.StakedSoulRaw,
                SOUL_SUPPLY_RAW = snapshot.SoulSupplyRaw,
                STAKERS_COUNT = snapshot.StakersCount,
                MASTERS_COUNT = snapshot.MastersCount,
                STAKING_RATIO = snapshot.StakingRatio,
                CAPTURED_AT_UNIX_SECONDS = snapshot.CapturedAtUnixSeconds,
                SOURCE = StakeSnapshotCatchupSource
            });

            inserted++;
        }

        if (inserted > 0)
            await databaseContext.SaveChangesAsync();

        Log.Information("[{Name}][Balances] Stake snapshot catch-up completed for {Chain}: inserted daily rows={Count}",
            nameof(PhantasmaPlugin), chain.NAME, inserted);
        return inserted;
    }

    private static async Task<List<SnapshotDailyPoint>> BuildMissingDailySnapshotsReverseAsync(string chainName,
        SnapshotCatchupState state, long nowUnixSeconds, long currentDayUnixSeconds, long missingFromDayUnixSeconds)
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
                        AND e."PAYLOAD_JSON"->'token_event'->>'token' = 'SOUL'
                    )
                    OR
                    (
                        ek."NAME" IN ('OrganizationAdd', 'OrganizationRemove')
                        AND e."PAYLOAD_FORMAT" IN ('legacy.backfill.v1', 'live.v1')
                    )
                    OR
                    (
                        ek."NAME" IN ('OrderCreated', 'OrderCancelled', 'OrderFilled', 'OrderClosed', 'OrderBid')
                        AND e."PAYLOAD_FORMAT" IN ('legacy.backfill.v1', 'live.v1')
                    )
              )
            ORDER BY
                ts DESC,
                block_height DESC,
                tx_index DESC,
                event_index DESC,
                event_id DESC;
            """;

        var snapshots = new SortedDictionary<long, SnapshotDailyPoint>();
        var currentDay = currentDayUnixSeconds;

        var connectionString = MainDbContext.GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = 0;
        cmd.Parameters.AddWithValue("chainName", chainName);
        cmd.Parameters.AddWithValue("fromTs", missingFromDayUnixSeconds);
        cmd.Parameters.AddWithValue("toTs", nowUnixSeconds);

        int? currentTxId = null;
        var txRows = new List<SnapshotEventRow>(32);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new SnapshotEventRow(
                EventId: reader.GetInt32(0),
                TxId: reader.GetInt32(1),
                Kind: reader.GetString(2),
                TimestampUnixSeconds: reader.GetInt64(3),
                PayloadFormat: reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                PayloadJson: reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                PayloadIdentity: reader.IsDBNull(9) ? (reader.IsDBNull(8) ? string.Empty : reader.GetString(8)) : reader.GetString(9)
            );

            if (currentTxId.HasValue && currentTxId.Value != row.TxId)
            {
                currentDay = ProcessTransactionForSnapshotCatchupReverse(txRows, state, snapshots, currentDay,
                    missingFromDayUnixSeconds);
                txRows.Clear();
            }

            currentTxId = row.TxId;
            txRows.Add(row);
        }

        if (txRows.Count > 0)
        {
            currentDay = ProcessTransactionForSnapshotCatchupReverse(txRows, state, snapshots, currentDay,
                missingFromDayUnixSeconds);
        }

        while (currentDay > missingFromDayUnixSeconds)
        {
            currentDay -= SecondsPerDay;
            if (!snapshots.ContainsKey(currentDay))
                snapshots[currentDay] = BuildSnapshotDailyPoint(currentDay, DayEnd(currentDay), state);
        }

        return snapshots.Values.OrderBy(x => x.DateUnixSeconds).ToList();
    }

    private static long ProcessTransactionForSnapshotCatchupReverse(List<SnapshotEventRow> txRows,
        SnapshotCatchupState state, SortedDictionary<long, SnapshotDailyPoint> snapshots, long currentDay,
        long missingFromDayUnixSeconds)
    {
        if (txRows.Count == 0)
            return currentDay;

        var deduplicatedRows = DeduplicateSnapshotTxRows(txRows);
        var stakeClaimType = ClassifySnapshotStakeClaimType(deduplicatedRows);

        var pendingStakerRemovals = new HashSet<string>(StringComparer.Ordinal);
        var pendingMasterRemovals = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in deduplicatedRows)
        {
            var eventDay = DayStart(row.TimestampUnixSeconds);
            while (eventDay < currentDay && currentDay > missingFromDayUnixSeconds)
            {
                currentDay -= SecondsPerDay;
                if (!snapshots.ContainsKey(currentDay))
                    snapshots[currentDay] = BuildSnapshotDailyPoint(currentDay, DayEnd(currentDay), state);
            }

            if (row.Kind == "OrganizationAdd" || row.Kind == "OrganizationRemove")
            {
                if (!TryParseStructuredOrganizationPayload(row.PayloadJson, out var organization, out var memberAddress))
                    continue;

                if (!IsTargetOrganizationName(organization))
                    continue;

                if (string.Equals(organization, "stakers", StringComparison.OrdinalIgnoreCase))
                {
                    if (row.Kind == "OrganizationAdd")
                    {
                        pendingStakerRemovals.Add(memberAddress);
                    }
                    else
                    {
                        state.Stakers.Add(memberAddress);
                        if (!state.StakesByAddress.ContainsKey(memberAddress))
                            state.StakesByAddress[memberAddress] = BigInteger.Zero;
                    }
                }
                else
                {
                    if (row.Kind == "OrganizationAdd")
                        pendingMasterRemovals.Add(memberAddress);
                    else
                        state.Masters.Add(memberAddress);
                }

                continue;
            }

            if (row.Kind != "TokenStake" && row.Kind != "TokenClaim" && row.Kind != "TokenMint" &&
                row.Kind != "TokenBurn")
                continue;

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
                    if (string.IsNullOrWhiteSpace(address))
                        continue;

                    if (!state.Stakers.Contains(address) && !pendingStakerRemovals.Contains(address))
                        continue;

                    ApplyStakeDeltaReverseStrict(state, address, -valueRaw);
                    break;

                case "TokenClaim":
                    if (string.IsNullOrWhiteSpace(address))
                        continue;

                    if (!state.Stakers.Contains(address) && !pendingStakerRemovals.Contains(address))
                        continue;

                    ApplyStakeDeltaReverseStrict(state, address, valueRaw);
                    break;

                case "TokenMint":
                    state.SoulSupplyRaw -= valueRaw;
                    if (state.SoulSupplyRaw < 0)
                        throw new InvalidOperationException(
                            $"Stake snapshot catch-up produced negative SOUL supply at event {row.EventId}");
                    break;

                case "TokenBurn":
                    state.SoulSupplyRaw += valueRaw;
                    break;
            }
        }

        FlushSnapshotPendingOrgAddReversals(state, pendingStakerRemovals, pendingMasterRemovals);
        return currentDay;
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

            if (!tokenEvent.TryGetProperty("token", out var tokenElement) || tokenElement.ValueKind != JsonValueKind.String)
                return false;

            tokenSymbol = tokenElement.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(tokenSymbol))
                return false;

            if (!tokenEvent.TryGetProperty("value_raw", out var valueElement) || valueElement.ValueKind != JsonValueKind.String)
                return false;

            var valueText = valueElement.GetString();
            return BigInteger.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueRaw);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseStructuredOrganizationPayload(string payloadJson, out string organization,
        out string memberAddress)
    {
        organization = string.Empty;
        memberAddress = string.Empty;

        if (string.IsNullOrWhiteSpace(payloadJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("organization_event", out var orgEvent) ||
                orgEvent.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!orgEvent.TryGetProperty("organization", out var orgElement) ||
                orgElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            if (!orgEvent.TryGetProperty("address", out var memberElement) || memberElement.ValueKind != JsonValueKind.String)
                return false;

            organization = orgElement.GetString() ?? string.Empty;
            memberAddress = memberElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(organization) && !string.IsNullOrWhiteSpace(memberAddress);
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

    private static bool IsTargetOrganizationName(string organization)
    {
        return string.Equals(organization, "stakers", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(organization, "masters", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyStakeDeltaReverseStrict(SnapshotCatchupState state, string address, BigInteger delta)
    {
        state.StakesByAddress.TryGetValue(address, out var oldValue);
        var newValue = oldValue + delta;
        if (newValue < 0)
        {
            throw new InvalidOperationException(
                $"Stake snapshot catch-up produced negative staked amount for {address}: old={oldValue}, delta={delta}, new={newValue}");
        }

        state.StakesByAddress[address] = newValue;
        state.TotalStakedRaw += newValue - oldValue;

        if (state.TotalStakedRaw < 0)
        {
            throw new InvalidOperationException(
                $"Stake snapshot catch-up produced negative total staked value: {state.TotalStakedRaw}");
        }
    }

    private static void FlushSnapshotPendingOrgAddReversals(SnapshotCatchupState state,
        HashSet<string> pendingStakerRemovals, HashSet<string> pendingMasterRemovals)
    {
        foreach (var address in pendingMasterRemovals)
            state.Masters.Remove(address);

        foreach (var address in pendingStakerRemovals)
        {
            if (!state.Stakers.Remove(address))
                continue;

            if (state.StakesByAddress.TryGetValue(address, out var stakeValue))
            {
                state.TotalStakedRaw -= stakeValue;
                if (state.TotalStakedRaw < 0)
                {
                    throw new InvalidOperationException(
                        $"Stake snapshot catch-up produced negative total while reversing staker add for {address}");
                }

                state.StakesByAddress.Remove(address);
            }
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
            StakersCount: state.Stakers.Count,
            MastersCount: state.Masters.Count,
            StakingRatio: stakingRatio,
            CapturedAtUnixSeconds: capturedAtUnixSeconds
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

        if (!BigInteger.TryParse(soulSupplyRawText, out var soulSupplyRaw) || soulSupplyRaw <= 0)
            throw new InvalidOperationException("Cannot load current SOUL supply for stake snapshot catch-up.");

        state.SoulSupplyRaw = soulSupplyRaw;

        var stakersOrgId = await databaseContext.Organizations.AsNoTracking()
            .Where(x => x.NAME.ToLower() == "stakers")
            .Select(x => (int?)x.ID)
            .FirstOrDefaultAsync();

        if (stakersOrgId.HasValue)
        {
            var stakerRows = await databaseContext.OrganizationAddresses.AsNoTracking()
                .Where(x => x.OrganizationId == stakersOrgId.Value &&
                            x.Address.ChainId == chainId &&
                            x.Address.ADDRESS != "NULL")
                .Select(x => new { x.Address.ADDRESS, x.Address.STAKED_AMOUNT_RAW })
                .ToListAsync();

            foreach (var stakerRow in stakerRows)
            {
                var stakeValue = BigInteger.Zero;
                if (!string.IsNullOrWhiteSpace(stakerRow.STAKED_AMOUNT_RAW))
                    BigInteger.TryParse(stakerRow.STAKED_AMOUNT_RAW, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out stakeValue);

                if (stakeValue < 0)
                    stakeValue = BigInteger.Zero;

                state.Stakers.Add(stakerRow.ADDRESS);
                state.StakesByAddress[stakerRow.ADDRESS] = stakeValue;
                state.TotalStakedRaw += stakeValue;
            }
        }

        var mastersOrgId = await databaseContext.Organizations.AsNoTracking()
            .Where(x => x.NAME.ToLower() == "masters")
            .Select(x => (int?)x.ID)
            .FirstOrDefaultAsync();

        if (mastersOrgId.HasValue)
        {
            var masterAddresses = await databaseContext.OrganizationAddresses.AsNoTracking()
                .Where(x => x.OrganizationId == mastersOrgId.Value &&
                            x.Address.ChainId == chainId &&
                            x.Address.ADDRESS != "NULL")
                .Select(x => x.Address.ADDRESS)
                .ToListAsync();

            foreach (var masterAddress in masterAddresses)
                state.Masters.Add(masterAddress);
        }

        return state;
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
