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


    public static async Task<EventKind> UpsertAsync(MainDbContext databaseContext, Chain chain, string name)
    {
        var entry = await databaseContext.EventKinds.FirstOrDefaultAsync(x => x.Chain == chain && x.NAME == name);
        if ( entry != null ) return entry;

        // Checking if entry has been added already
        // but not yet inserted into database.
        entry = DbHelper.GetTracked<EventKind>(databaseContext)
            .FirstOrDefault(x => x.Chain == chain && x.NAME == name);
        if ( entry != null ) return entry;
        
        entry = new EventKind {Chain = chain, NAME = name};
        await databaseContext.EventKinds.AddAsync(entry);

        return entry;
    }


    public static Task<EventKind> GetByNameAsync(MainDbContext databaseContext, Chain chain, string name)
    {
        return databaseContext.EventKinds.FirstOrDefaultAsync(x => x.Chain == chain && x.NAME == name);
    }
}
