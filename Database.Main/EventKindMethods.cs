using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class EventKindMethods
{
    // Checks if "EventKinds" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.


    public static int Upsert(MainDbContext databaseContext, Chain chain, string name, bool saveChanges = true)
    {
        var entry = databaseContext.EventKinds.FirstOrDefault(x => x.Chain == chain && x.NAME == name);
        if ( entry != null ) return entry.ID;

        entry = new EventKind {Chain = chain, NAME = name};
        databaseContext.EventKinds.Add(entry);

        if ( !saveChanges ) return entry.ID;

        try
        {
            databaseContext.SaveChanges();
        }
        catch ( Exception ex )
        {
            // TODO should not happen since GetTrackedObjects() is called,
            // leaving it here just in case.
            // This catch not helping for async code structure of EVM plugin.
            var exMessage = ex.ToString();
            if ( exMessage.Contains("duplicate key value violates unique constraint") &&
                 exMessage.Contains("IX_EventKinds_ChainId_NAME") )
            {
                // We tried to create same event in two threads concurrently.
                // Now we should just remove duplicating event and get an existing event.
                databaseContext.EventKinds.Remove(entry);
                entry = databaseContext.EventKinds.First(x =>
                    x.Chain == chain && x.NAME == name);
            }
            else
                // Unknown exception.
                throw;
        }

        return entry.ID;
    }


    public static Task<EventKind> GetByNameAsync(MainDbContext databaseContext, Chain chain, string name)
    {
        return databaseContext.EventKinds.FirstOrDefaultAsync(x => x.Chain == chain && x.NAME == name);
    }


    public static EventKind GetById(MainDbContext databaseContext, int id)
    {
        return databaseContext.EventKinds.FirstOrDefault(x => x.ID == id);
    }
}
