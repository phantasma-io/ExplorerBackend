using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class EventKindMethods
{
    public static async Task UpsertAllAsync(MainDbContext dbContext, Chain chain)
    {
        foreach (var kind in Enum.GetValues<Phantasma.Core.Domain.Events.Structs.EventKind>())
        {
            if (!dbContext.EventKinds.Any(e => e.Chain.ID == chain.ID && e.NAME == kind.ToString()))
            {
                dbContext.EventKinds.Add(new EventKind {Chain = chain, NAME = kind.ToString()});
            }
        }
    }

    public readonly record struct ChainEventKindKey(int ChainId, Phantasma.Core.Domain.Events.Structs.EventKind Kind);
    public static async Task<Dictionary<ChainEventKindKey, int>> GetAllAsync(MainDbContext dbContext)
    {
        return await dbContext.EventKinds
            .ToDictionaryAsync(
                e => new ChainEventKindKey(e.ChainId, Enum.Parse<Phantasma.Core.Domain.Events.Structs.EventKind>(e.NAME)),
                e => e.ID
            );
    }
}

public static class EventKindMethodsExtensions
{
    public static int GetId(this Dictionary<EventKindMethods.ChainEventKindKey, int> eventKinds, int chainId, Phantasma.Core.Domain.Events.Structs.EventKind kind)
    {
        return eventKinds.Where(x => x.Key.ChainId == chainId && x.Key.Kind == kind).Select(x => x.Value).First();
    }
}
