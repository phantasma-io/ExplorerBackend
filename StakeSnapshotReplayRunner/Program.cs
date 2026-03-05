using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using Backend.Blockchain;
using Database.Main;
using Microsoft.EntityFrameworkCore;

internal static class Program
{
    private const long SecondsPerDay = 86400;

    private sealed record RunnerOptions(
        string Chain,
        long? FromDayUnixSeconds,
        long? AnchorDayUnixSeconds,
        long? ToUnixSeconds,
        int TopTransactions,
        bool Apply
    );

    private sealed record SnapshotStateView(
        BigInteger TotalStakedRaw,
        BigInteger SoulSupplyRaw,
        int StakersCount,
        int MastersCount
    );

    private sealed record ReverseDelta(
        int TxId,
        long TimestampUnixSeconds,
        int FirstEventId,
        int Rows,
        BigInteger DeltaStakedRaw
    );

    private sealed record TransactionMeta(
        int TxId,
        string Hash,
        long TimestampUnixSeconds,
        long BlockHeight,
        string StateName
    );

    private sealed record ReconstructedDailyPoint(
        long DayUnixSeconds,
        BigInteger StakedRaw,
        BigInteger SoulSupplyRaw,
        int StakersCount,
        int MastersCount
    );

    private sealed record ExistingDailyPoint(
        long DayUnixSeconds,
        BigInteger StakedRaw,
        BigInteger SoulSupplyRaw,
        int StakersCount,
        int MastersCount,
        string Source
    );

    public static async Task<int> Main(string[] args)
    {
        RunnerOptions? options;
        try
        {
            options = ParseArgs(args);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.Message);
            Console.WriteLine();
            PrintUsage();
            return 1;
        }

        if (options == null)
            return 0;

        var pluginType = typeof(PhantasmaPlugin);
        var detectRangeMethod = RequireStaticMethod(pluginType, "DetectSnapshotRebuildRangeAsync", 3);
        var loadAnchorMethod = RequireStaticMethod(pluginType, "LoadSnapshotAnchorPointAsync", 3);
        var loadCurrentStateMethod = RequireStaticMethod(pluginType, "LoadCurrentSnapshotStateAsync", 2);
        var loadEventsMethod = RequireStaticMethod(pluginType, "LoadSnapshotEventsAsync", 3);
        var applyReverseMethod = RequireStaticMethod(pluginType, "ApplySnapshotTransactionReverse", 2);
        var buildForwardMethod = RequireStaticMethod(pluginType, "BuildDailySnapshotsForward", 4);
        var matchesAnchorMethod = RequireStaticMethod(pluginType, "MatchesSnapshotAnchor", 3);
        var backfillSnapshotsMethod = RequireStaticMethod(pluginType, "BackfillMissingStakeSnapshotsAsync", 5);

        await using var db = new MainDbContext();
        var chain = await db.Chains.AsNoTracking().FirstOrDefaultAsync(x => x.NAME == options.Chain);
        if (chain == null)
        {
            Console.WriteLine($"Chain '{options.Chain}' was not found.");
            return 2;
        }

        var nowUnixSeconds = options.ToUnixSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var currentDayUnixSeconds = DayStart(nowUnixSeconds);

        var rebuildRange = await ResolveRebuildRangeAsync(
            pluginType,
            detectRangeMethod,
            db,
            chain.ID,
            currentDayUnixSeconds,
            options);
        if (rebuildRange == null)
        {
            Console.WriteLine("No rebuild range was detected. Nothing to replay.");
            return 0;
        }

        var anchorDay = ReadLongMember(rebuildRange, "AnchorDayUnixSeconds");
        var rebuildFromDay = ReadLongMember(rebuildRange, "RebuildFromDayUnixSeconds");
        var rebuildToExclusiveDay = ReadLongMember(rebuildRange, "RebuildToExclusiveDayUnixSeconds");
        var reason = ReadStringMember(rebuildRange, "Reason");

        var anchor = await InvokeStaticAsync(loadAnchorMethod, db, chain.ID, anchorDay);
        if (anchor == null)
        {
            Console.WriteLine($"Anchor row is missing at {FormatUtc(anchorDay)}.");
            return 3;
        }

        var currentState = await InvokeStaticAsync(loadCurrentStateMethod, db, chain.ID);
        if (currentState == null)
        {
            Console.WriteLine("Failed to load current snapshot state.");
            return 4;
        }

        var currentBeforeReverse = ReadState(currentState);
        var eventRowsObject = await InvokeStaticAsync(loadEventsMethod, chain.NAME, rebuildFromDay, nowUnixSeconds);
        if (eventRowsObject is not IList eventRows)
        {
            Console.WriteLine("Failed to load snapshot events.");
            return 5;
        }

        var reverseDeltas = ReplayReverseAndCollectDeltas(currentState, eventRows, applyReverseMethod);
        var currentAfterReverse = ReadState(currentState);

        object?[] matchesArgs = [currentState, anchor, string.Empty];
        var isMatch = (bool)(matchesAnchorMethod.Invoke(null, matchesArgs) ?? false);
        var mismatchReason = matchesArgs[2] as string ?? string.Empty;

        var anchorState = new SnapshotStateView(
            ReadBigIntegerMember(anchor, "StakedSoulRaw"),
            ReadBigIntegerMember(anchor, "SoulSupplyRaw"),
            ReadIntMember(anchor, "StakersCount"),
            ReadIntMember(anchor, "MastersCount"));

        PrintSummary(
            chain.NAME,
            reason,
            rebuildFromDay,
            rebuildToExclusiveDay,
            anchorDay,
            nowUnixSeconds,
            eventRows.Count,
            reverseDeltas.Count,
            currentBeforeReverse,
            currentAfterReverse,
            anchorState,
            isMatch,
            mismatchReason);

        var reconstructedRows = ReadReconstructedDailyRows(buildForwardMethod, currentState, eventRows,
            rebuildFromDay, rebuildToExclusiveDay);
        PrintReconstructedSamples(reconstructedRows);

        var existingRows = await LoadExistingDailyRowsAsync(db, chain.ID, anchorDay, rebuildToExclusiveDay);
        PrintExistingDailySamples(existingRows, rebuildFromDay);

        PrintBoundaryAudit(existingRows, reconstructedRows, eventRows, rebuildFromDay, rebuildToExclusiveDay);

        if (!isMatch)
            await PrintTopReverseDeltasAsync(db, reverseDeltas, options.TopTransactions);

        if (options.Apply)
        {
            var currentMonthUnixSeconds = MonthStart(nowUnixSeconds);
            var applyResult = await InvokeStaticAsync(backfillSnapshotsMethod, db, chain, nowUnixSeconds,
                currentDayUnixSeconds, currentMonthUnixSeconds);

            var dailyAffected = applyResult == null ? 0 : ReadIntMember(applyResult, "DailyInserted");
            var monthlyAffected = applyResult == null ? 0 : ReadIntMember(applyResult, "MonthlyInserted");

            Console.WriteLine();
            Console.WriteLine(
                $"APPLY RESULT: daily_affected={dailyAffected}, monthly_affected={monthlyAffected}, upper_ts={FormatUtc(nowUnixSeconds)}");

            return 0;
        }

        return isMatch ? 0 : 6;
    }

    private static List<ReconstructedDailyPoint> ReadReconstructedDailyRows(
        MethodInfo buildForwardMethod,
        object currentState,
        IList eventRows,
        long rebuildFromDay,
        long rebuildToExclusiveDay)
    {
        var forward = buildForwardMethod.Invoke(null, [currentState, eventRows, rebuildFromDay, rebuildToExclusiveDay]);
        if (forward is not IList forwardRows || forwardRows.Count == 0)
            return [];

        var result = new List<ReconstructedDailyPoint>(forwardRows.Count);
        foreach (var row in forwardRows.Cast<object>())
        {
            result.Add(new ReconstructedDailyPoint(
                DayUnixSeconds: ReadLongMember(row, "DateUnixSeconds"),
                StakedRaw: ReadBigIntegerMember(row, "StakedSoulRaw"),
                SoulSupplyRaw: ReadBigIntegerMember(row, "SoulSupplyRaw"),
                StakersCount: ReadIntMember(row, "StakersCount"),
                MastersCount: ReadIntMember(row, "MastersCount")));
        }

        return result;
    }

    private static void PrintReconstructedSamples(List<ReconstructedDailyPoint> reconstructedRows)
    {
        if (reconstructedRows.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("Reconstructed daily samples: no rows.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Reconstructed daily samples:");

        foreach (var row in reconstructedRows.Take(3))
        {
            Console.WriteLine(
                $"  first day={FormatUtc(row.DayUnixSeconds)} staked_raw={row.StakedRaw} supply_raw={row.SoulSupplyRaw} " +
                $"stakers={row.StakersCount} masters={row.MastersCount}");
        }

        if (reconstructedRows.Count > 6)
            Console.WriteLine("  ...");

        foreach (var row in reconstructedRows.Skip(Math.Max(0, reconstructedRows.Count - 3)))
        {
            Console.WriteLine(
                $"  last  day={FormatUtc(row.DayUnixSeconds)} staked_raw={row.StakedRaw} supply_raw={row.SoulSupplyRaw} " +
                $"stakers={row.StakersCount} masters={row.MastersCount}");
        }
    }

    private static async Task<List<ExistingDailyPoint>> LoadExistingDailyRowsAsync(
        MainDbContext db,
        int chainId,
        long fromDayInclusive,
        long toExclusiveDay)
    {
        var rows = await db.StakingProgressDailies.AsNoTracking()
            .Where(x => x.ChainId == chainId &&
                        x.DATE_UNIX_SECONDS >= fromDayInclusive &&
                        x.DATE_UNIX_SECONDS < toExclusiveDay)
            .OrderBy(x => x.DATE_UNIX_SECONDS)
            .Select(x => new
            {
                x.DATE_UNIX_SECONDS,
                x.STAKED_SOUL_RAW,
                x.SOUL_SUPPLY_RAW,
                x.STAKERS_COUNT,
                x.MASTERS_COUNT,
                x.SOURCE
            })
            .ToListAsync();

        var parsed = new List<ExistingDailyPoint>(rows.Count);
        foreach (var row in rows)
        {
            if (!BigInteger.TryParse(row.STAKED_SOUL_RAW, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var stakedRaw))
            {
                throw new InvalidOperationException(
                    $"Cannot parse STAKED_SOUL_RAW for day={FormatUtc(row.DATE_UNIX_SECONDS)}.");
            }

            if (!BigInteger.TryParse(row.SOUL_SUPPLY_RAW, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var supplyRaw))
            {
                throw new InvalidOperationException(
                    $"Cannot parse SOUL_SUPPLY_RAW for day={FormatUtc(row.DATE_UNIX_SECONDS)}.");
            }

            parsed.Add(new ExistingDailyPoint(
                DayUnixSeconds: row.DATE_UNIX_SECONDS,
                StakedRaw: stakedRaw,
                SoulSupplyRaw: supplyRaw,
                StakersCount: row.STAKERS_COUNT,
                MastersCount: row.MASTERS_COUNT,
                Source: row.SOURCE ?? string.Empty));
        }

        return parsed;
    }

    private static void PrintExistingDailySamples(List<ExistingDailyPoint> existingRows, long rebuildFromDay)
    {
        if (existingRows.Count == 0)
        {
            Console.WriteLine();
            Console.WriteLine("Existing daily samples: no rows.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Existing daily samples:");

        var firstRows = existingRows.Take(3).ToList();
        foreach (var row in firstRows)
        {
            Console.WriteLine(
                $"  first day={FormatUtc(row.DayUnixSeconds)} staked_raw={row.StakedRaw} supply_raw={row.SoulSupplyRaw} " +
                $"stakers={row.StakersCount} masters={row.MastersCount} source={row.Source}");
        }

        if (existingRows.Count > 6)
            Console.WriteLine("  ...");

        foreach (var row in existingRows.Skip(Math.Max(0, existingRows.Count - 3)))
        {
            Console.WriteLine(
                $"  last  day={FormatUtc(row.DayUnixSeconds)} staked_raw={row.StakedRaw} supply_raw={row.SoulSupplyRaw} " +
                $"stakers={row.StakersCount} masters={row.MastersCount} source={row.Source}");
        }

        var rebuildStartRow = existingRows.FirstOrDefault(x => x.DayUnixSeconds == rebuildFromDay);
        if (rebuildStartRow != null)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  rebuild_start day={FormatUtc(rebuildStartRow.DayUnixSeconds)} staked_raw={rebuildStartRow.StakedRaw} " +
                $"supply_raw={rebuildStartRow.SoulSupplyRaw} stakers={rebuildStartRow.StakersCount} " +
                $"masters={rebuildStartRow.MastersCount} source={rebuildStartRow.Source}");
        }
    }

    private static void PrintBoundaryAudit(
        List<ExistingDailyPoint> existingRows,
        List<ReconstructedDailyPoint> reconstructedRows,
        IList eventRows,
        long rebuildFromDay,
        long rebuildToExclusiveDay)
    {
        if (existingRows.Count == 0)
            return;

        var replayEventCountByDay = new Dictionary<long, int>();
        foreach (var row in eventRows.Cast<object>())
        {
            var day = DayStart(ReadLongMember(row, "TimestampUnixSeconds"));
            replayEventCountByDay.TryGetValue(day, out var currentCount);
            replayEventCountByDay[day] = currentCount + 1;
        }

        Console.WriteLine();
        Console.WriteLine("Source boundary + no-event jump audit:");

        var printedAnyBoundary = false;
        for (var i = 1; i < existingRows.Count; i++)
        {
            var previous = existingRows[i - 1];
            var current = existingRows[i];
            var day = current.DayUnixSeconds;
            if (day < rebuildFromDay || day >= rebuildToExclusiveDay)
                continue;

            var deltaStaked = current.StakedRaw - previous.StakedRaw;
            var deltaSupply = current.SoulSupplyRaw - previous.SoulSupplyRaw;
            var deltaStakers = current.StakersCount - previous.StakersCount;
            var deltaMasters = current.MastersCount - previous.MastersCount;
            var replayEvents = replayEventCountByDay.GetValueOrDefault(day, 0);

            var isSourceBoundary = !string.Equals(previous.Source, current.Source, StringComparison.Ordinal);
            var isNoEventJump = replayEvents == 0 &&
                                (deltaStaked != BigInteger.Zero ||
                                 deltaSupply != BigInteger.Zero ||
                                 deltaStakers != 0 ||
                                 deltaMasters != 0);
            if (!isSourceBoundary && !isNoEventJump)
                continue;

            printedAnyBoundary = true;
            Console.WriteLine(
                $"  day={FormatUtc(day)} source={previous.Source} -> {current.Source} replay_events={replayEvents} " +
                $"delta_staked_raw={deltaStaked} delta_supply_raw={deltaSupply} " +
                $"delta_stakers={deltaStakers} delta_masters={deltaMasters}");
        }

        if (!printedAnyBoundary)
            Console.WriteLine("  no source boundaries or no-event jumps were detected.");

        if (reconstructedRows.Count == 0)
            return;

        var reconstructedByDay = reconstructedRows.ToDictionary(x => x.DayUnixSeconds);
        var comparable = existingRows
            .Where(x => x.DayUnixSeconds >= rebuildFromDay &&
                        x.DayUnixSeconds < rebuildToExclusiveDay &&
                        reconstructedByDay.ContainsKey(x.DayUnixSeconds))
            .Select(x =>
            {
                var reconstructed = reconstructedByDay[x.DayUnixSeconds];
                return new
                {
                    Day = x.DayUnixSeconds,
                    x.Source,
                    DeltaStaked = x.StakedRaw - reconstructed.StakedRaw,
                    DeltaSupply = x.SoulSupplyRaw - reconstructed.SoulSupplyRaw,
                    DeltaStakers = x.StakersCount - reconstructed.StakersCount,
                    DeltaMasters = x.MastersCount - reconstructed.MastersCount
                };
            })
            .OrderBy(x => x.Day)
            .ToList();

        if (comparable.Count == 0)
            return;

        Console.WriteLine();
        Console.WriteLine("Existing minus reconstructed offset segments:");

        var segmentStartIndex = 0;
        for (var i = 1; i <= comparable.Count; i++)
        {
            var isSegmentEnd = i == comparable.Count ||
                               comparable[i].DeltaStaked != comparable[segmentStartIndex].DeltaStaked ||
                               comparable[i].DeltaSupply != comparable[segmentStartIndex].DeltaSupply ||
                               comparable[i].DeltaStakers != comparable[segmentStartIndex].DeltaStakers ||
                               comparable[i].DeltaMasters != comparable[segmentStartIndex].DeltaMasters;
            if (!isSegmentEnd)
                continue;

            var first = comparable[segmentStartIndex];
            var last = comparable[i - 1];
            Console.WriteLine(
                $"  {FormatUtc(first.Day)} .. {FormatUtc(last.Day)} " +
                $"delta_staked_raw={first.DeltaStaked} delta_supply_raw={first.DeltaSupply} " +
                $"delta_stakers={first.DeltaStakers} delta_masters={first.DeltaMasters} " +
                $"source_range={first.Source}->{last.Source}");

            segmentStartIndex = i;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project StakeSnapshotReplayRunner -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --chain <name>          Chain name (default: main).");
        Console.WriteLine("  --from-day <unix|date>  Force replay start day (UTC day start).");
        Console.WriteLine("  --anchor-day <unix|date> Force anchor day (UTC day start).");
        Console.WriteLine("  --to-ts <unix|date>     Override upper event timestamp bound (default: now UTC).");
        Console.WriteLine("  --top <count>           Top reverse tx deltas to print on mismatch (default: 20).");
        Console.WriteLine("  --apply                 Apply stake snapshot backfill to DB after diagnostics.");
        Console.WriteLine("  --help                  Show this help.");
        Console.WriteLine();
        Console.WriteLine("Date input accepts unix seconds or ISO timestamp (e.g. 2025-01-18T00:00:00Z).");
    }

    private static RunnerOptions? ParseArgs(string[] args)
    {
        var chain = "main";
        long? fromDay = null;
        long? anchorDay = null;
        long? toUnix = null;
        var top = 20;
        var apply = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    PrintUsage();
                    return null;
                case "--chain":
                    chain = ReadValue(args, ref i, arg);
                    break;
                case "--from-day":
                    fromDay = DayStart(ParseUnixArg(ReadValue(args, ref i, arg)));
                    break;
                case "--anchor-day":
                    anchorDay = DayStart(ParseUnixArg(ReadValue(args, ref i, arg)));
                    break;
                case "--to-ts":
                    toUnix = ParseUnixArg(ReadValue(args, ref i, arg));
                    break;
                case "--top":
                    top = int.Parse(ReadValue(args, ref i, arg), CultureInfo.InvariantCulture);
                    if (top <= 0)
                        throw new ArgumentOutOfRangeException(nameof(top), "--top must be > 0.");
                    break;
                case "--apply":
                    apply = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{arg}'. Use --help.");
            }
        }

        return new RunnerOptions(chain, fromDay, anchorDay, toUnix, top, apply);
    }

    private static string ReadValue(string[] args, ref int i, string optionName)
    {
        var next = i + 1;
        if (next >= args.Length)
            throw new ArgumentException($"Missing value for {optionName}.");

        i = next;
        return args[next];
    }

    private static long ParseUnixArg(string value)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
            return unix;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            return dto.ToUniversalTime().ToUnixTimeSeconds();

        throw new ArgumentException($"Cannot parse unix/date value '{value}'.");
    }

    private static async Task<object?> ResolveRebuildRangeAsync(
        Type pluginType,
        MethodInfo detectRangeMethod,
        MainDbContext db,
        int chainId,
        long currentDayUnixSeconds,
        RunnerOptions options)
    {
        if (!options.FromDayUnixSeconds.HasValue && !options.AnchorDayUnixSeconds.HasValue)
            return await InvokeStaticAsync(detectRangeMethod, db, chainId, currentDayUnixSeconds);

        var fromDay = options.FromDayUnixSeconds ?? options.AnchorDayUnixSeconds!.Value + SecondsPerDay;
        var anchorDay = options.AnchorDayUnixSeconds ?? fromDay - SecondsPerDay;
        var rangeType = RequireNestedType(pluginType, "SnapshotRebuildRange");

        return Activator.CreateInstance(
            rangeType,
            anchorDay,
            fromDay,
            currentDayUnixSeconds,
            "manual-range");
    }

    private static List<ReverseDelta> ReplayReverseAndCollectDeltas(
        object currentState,
        IList eventRows,
        MethodInfo applyReverseMethod)
    {
        var deltas = new List<ReverseDelta>();
        if (eventRows.Count == 0)
            return deltas;

        var rowType = eventRows[0]!.GetType();
        var typedListType = typeof(List<>).MakeGenericType(rowType);

        for (var txGroupEnd = eventRows.Count - 1; txGroupEnd >= 0;)
        {
            var txId = ReadIntMember(eventRows[txGroupEnd]!, "TxId");
            var txGroupStart = txGroupEnd;
            while (txGroupStart > 0 && ReadIntMember(eventRows[txGroupStart - 1]!, "TxId") == txId)
                txGroupStart--;

            var txRows = (IList)(Activator.CreateInstance(typedListType) ??
                                  throw new InvalidOperationException("Failed to create typed tx rows list."));
            for (var i = txGroupStart; i <= txGroupEnd; i++)
                txRows.Add(eventRows[i]!);

            var before = ReadBigIntegerMember(currentState, "TotalStakedRaw");
            try
            {
                applyReverseMethod.Invoke(null, [currentState, txRows]);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw new InvalidOperationException(
                    $"Reverse replay failed on txId={txId} at ts={FormatUtc(ReadLongMember(eventRows[txGroupStart]!, "TimestampUnixSeconds"))}: {tie.InnerException.Message}",
                    tie.InnerException);
            }

            var after = ReadBigIntegerMember(currentState, "TotalStakedRaw");
            var delta = after - before;

            if (delta != BigInteger.Zero)
            {
                deltas.Add(new ReverseDelta(
                    TxId: txId,
                    TimestampUnixSeconds: ReadLongMember(eventRows[txGroupStart]!, "TimestampUnixSeconds"),
                    FirstEventId: ReadIntMember(eventRows[txGroupStart]!, "EventId"),
                    Rows: txRows.Count,
                    DeltaStakedRaw: delta));
            }

            txGroupEnd = txGroupStart - 1;
        }

        return deltas;
    }

    private static async Task PrintTopReverseDeltasAsync(
        MainDbContext db,
        List<ReverseDelta> reverseDeltas,
        int top)
    {
        if (reverseDeltas.Count == 0)
        {
            Console.WriteLine("No non-zero reverse tx deltas were produced.");
            return;
        }

        var topDeltas = reverseDeltas
            .OrderByDescending(x => BigInteger.Abs(x.DeltaStakedRaw))
            .Take(top)
            .ToList();

        var txIds = topDeltas.Select(x => x.TxId).Distinct().ToArray();
        var txMeta = await db.Transactions.AsNoTracking()
            .Where(x => txIds.Contains(x.ID))
            .Select(x => new TransactionMeta(
                x.ID,
                x.HASH,
                x.TIMESTAMP_UNIX_SECONDS,
                x.Block.HEIGHT,
                x.State.NAME))
            .ToDictionaryAsync(x => x.TxId);

        Console.WriteLine();
        Console.WriteLine($"Top {topDeltas.Count} reverse stake deltas by abs(raw):");
        foreach (var delta in topDeltas)
        {
            txMeta.TryGetValue(delta.TxId, out var meta);
            var txHash = meta?.Hash ?? "<unknown>";
            var stateName = meta?.StateName ?? "<unknown>";
            var blockHeight = meta?.BlockHeight.ToString(CultureInfo.InvariantCulture) ?? "<unknown>";
            Console.WriteLine(
                $"  txId={delta.TxId} hash={txHash} block={blockHeight} state={stateName} ts={FormatUtc(delta.TimestampUnixSeconds)} " +
                $"delta_raw={delta.DeltaStakedRaw} rows={delta.Rows} first_event_id={delta.FirstEventId}");
        }
    }

    private static void PrintSummary(
        string chainName,
        string reason,
        long fromDay,
        long toExclusiveDay,
        long anchorDay,
        long toUnixSeconds,
        int eventRows,
        int txDeltasCount,
        SnapshotStateView beforeReverse,
        SnapshotStateView afterReverse,
        SnapshotStateView anchor,
        bool isMatch,
        string mismatchReason)
    {
        Console.WriteLine($"Chain: {chainName}");
        Console.WriteLine($"Reason: {reason}");
        Console.WriteLine($"Range days: {FormatUtc(fromDay)} .. {FormatUtc(toExclusiveDay - SecondsPerDay)}");
        Console.WriteLine($"Anchor day: {FormatUtc(anchorDay)}");
        Console.WriteLine($"Events upper ts: {FormatUtc(toUnixSeconds)}");
        Console.WriteLine($"Loaded rows: events={eventRows}, tx_with_non_zero_delta={txDeltasCount}");
        Console.WriteLine();

        Console.WriteLine("State before reverse replay:");
        PrintState(beforeReverse);
        Console.WriteLine("State after reverse replay:");
        PrintState(afterReverse);
        Console.WriteLine("Anchor row:");
        PrintState(anchor);
        Console.WriteLine();

        var stakedGap = afterReverse.TotalStakedRaw - anchor.TotalStakedRaw;
        var supplyGap = afterReverse.SoulSupplyRaw - anchor.SoulSupplyRaw;
        var stakersGap = afterReverse.StakersCount - anchor.StakersCount;
        var mastersGap = afterReverse.MastersCount - anchor.MastersCount;

        Console.WriteLine("Delta (after_reverse - anchor):");
        Console.WriteLine($"  staked_raw={stakedGap}");
        Console.WriteLine($"  soul_supply_raw={supplyGap}");
        Console.WriteLine($"  stakers={stakersGap}");
        Console.WriteLine($"  masters={mastersGap}");
        Console.WriteLine();

        if (isMatch)
        {
            Console.WriteLine("MATCH: reverse replay converged to anchor.");
        }
        else
        {
            Console.WriteLine($"MISMATCH: {mismatchReason}");
        }
    }

    private static void PrintState(SnapshotStateView state)
    {
        Console.WriteLine($"  staked_raw={state.TotalStakedRaw}");
        Console.WriteLine($"  soul_supply_raw={state.SoulSupplyRaw}");
        Console.WriteLine($"  stakers={state.StakersCount}");
        Console.WriteLine($"  masters={state.MastersCount}");
    }

    private static SnapshotStateView ReadState(object state)
    {
        return new SnapshotStateView(
            ReadBigIntegerMember(state, "TotalStakedRaw"),
            ReadBigIntegerMember(state, "SoulSupplyRaw"),
            ReadIntMember(state, "StakersCount"),
            ReadIntMember(state, "MastersCount"));
    }

    private static long DayStart(long unixSeconds)
    {
        return unixSeconds - (unixSeconds % SecondsPerDay);
    }

    private static long MonthStart(long unixSeconds)
    {
        var date = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        return new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
    }

    private static string FormatUtc(long unixSeconds)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
    }

    private static Type RequireNestedType(Type owner, string nestedTypeName)
    {
        return owner.GetNestedType(nestedTypeName, BindingFlags.NonPublic)
               ?? throw new MissingMemberException(owner.FullName, nestedTypeName);
    }

    private static MethodInfo RequireStaticMethod(Type owner, string methodName, int parameterCount)
    {
        return owner.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                   .SingleOrDefault(m => m.Name == methodName && m.GetParameters().Length == parameterCount)
               ?? throw new MissingMethodException(owner.FullName, $"{methodName}({parameterCount} params)");
    }

    private static async Task<object?> InvokeStaticAsync(MethodInfo method, params object?[] args)
    {
        var invocation = method.Invoke(null, args);
        if (invocation is not Task task)
            return invocation;

        await task.ConfigureAwait(false);
        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task);
    }

    private static BigInteger ReadBigIntegerMember(object source, string memberName)
    {
        var value = ReadMember(source, memberName);
        return value switch
        {
            BigInteger big => big,
            string text when BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new InvalidCastException($"Member '{memberName}' is not BigInteger-compatible (type {value.GetType().FullName}).")
        };
    }

    private static int ReadIntMember(object source, string memberName)
    {
        var value = ReadMember(source, memberName);
        return value switch
        {
            int i => i,
            long l => checked((int)l),
            _ => Convert.ToInt32(value, CultureInfo.InvariantCulture)
        };
    }

    private static long ReadLongMember(object source, string memberName)
    {
        var value = ReadMember(source, memberName);
        return value switch
        {
            long l => l,
            int i => i,
            _ => Convert.ToInt64(value, CultureInfo.InvariantCulture)
        };
    }

    private static string ReadStringMember(object source, string memberName)
    {
        var value = ReadMember(source, memberName);
        return value?.ToString() ?? string.Empty;
    }

    private static object ReadMember(object source, string memberName)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var sourceType = source.GetType();

        var property = sourceType.GetProperty(memberName, flags);
        if (property != null)
            return property.GetValue(source)
                   ?? throw new InvalidOperationException($"Member '{memberName}' is null on type '{sourceType.FullName}'.");

        var field = sourceType.GetField(memberName, flags);
        if (field != null)
            return field.GetValue(source)
                   ?? throw new InvalidOperationException($"Member '{memberName}' is null on type '{sourceType.FullName}'.");

        throw new MissingMemberException(sourceType.FullName, memberName);
    }
}
