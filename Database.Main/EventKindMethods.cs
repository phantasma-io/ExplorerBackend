using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Database.Main;

public static class EventKindMethods
{
    private static bool _sequenceAligned = false;

    private static void EnsureSequence(MainDbContext dbContext)
    {
        if (_sequenceAligned)
        {
            return;
        }

        // Align serial/identity sequence with current max(ID) to avoid duplicate key errors
        const string sql =
            "SELECT setval(pg_get_serial_sequence('\"EventKinds\"','ID'), COALESCE(MAX(\"ID\"),0)+1, false) FROM \"EventKinds\";";
        dbContext.Database.ExecuteSqlRaw(sql);

        _sequenceAligned = true;
    }

    public static async Task UpsertAllAsync(MainDbContext dbContext, Chain chain)
    {
        EnsureSequence(dbContext);

        foreach (var kind in Enum.GetValues<PhantasmaPhoenix.Protocol.EventKind>())
        {
            if (!dbContext.EventKinds.Any(e => e.Chain.ID == chain.ID && e.NAME == kind.ToString()))
            {
                dbContext.EventKinds.Add(new EventKind {Chain = chain, NAME = kind.ToString()});
            }
        }
    }

    public readonly record struct ChainEventKindKey(int ChainId, PhantasmaPhoenix.Protocol.EventKind Kind);
    public static async Task<Dictionary<ChainEventKindKey, int>> GetAllAsync(MainDbContext dbContext)
    {
        var result = new Dictionary<ChainEventKindKey, int>();

        var items = await dbContext.EventKinds.ToListAsync();

        foreach (var e in items)
        {
            var key = new ChainEventKindKey(e.ChainId, Enum.Parse<PhantasmaPhoenix.Protocol.EventKind>(e.NAME));
            Log.Information("Loading EventKind {chain}/{name}", key.ChainId, key.Kind.ToString());

            if (!result.ContainsKey(key))
            {
                result[key] = e.ID;
            }
        }

        return result;
    }
}

public static class EventKindMethodsExtensions
{
    public static int GetId(this Dictionary<EventKindMethods.ChainEventKindKey, int> eventKinds, int chainId, PhantasmaPhoenix.Protocol.EventKind kind)
    {
        return eventKinds.Where(x => x.Key.ChainId == chainId && x.Key.Kind == kind).Select(x => x.Value).First();
    }
}
