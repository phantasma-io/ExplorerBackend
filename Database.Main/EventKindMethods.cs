using System;
using System.Linq;

namespace Database.Main;

public static class EventKindMethods
{
    // Checks if "EventKinds" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static int Upsert(MainDbContext databaseContext, int chainId, string name)
    {
        int id;
        var entry = databaseContext.EventKinds
            .FirstOrDefault(x => x.ChainId == chainId &&
                                 string.Equals(x.NAME.ToUpper(), name.ToUpper()));

        /*if (entry == null)
        {
            // Checking if entry has been added already
            // but not yet inserted into database.
            entry = (EventKind)Utils.GetTrackedObjects(databaseContext, typeof(EventKind)).Where(x => ((EventKind)x).ChainId == chainId && ((EventKind)x).NAME.ToUpper() == name.ToUpper()).FirstOrDefault();
        }*/

        if ( entry != null )
            id = entry.ID;
        else
        {
            entry = new EventKind {ChainId = chainId, NAME = name};
            databaseContext.EventKinds.Add(entry);

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
                        x.ChainId == chainId && string.Equals(x.NAME.ToUpper(), name.ToUpper()));
                }
                else
                    // Unknown exception.
                    throw;
            }

            id = entry.ID;
        }

        return id;
    }
}
