using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.ApiCache;

public static class ChainMethods
{
    // Checks if "Chains" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static Chain Upsert(ApiCacheDbContext databaseContext, string shortName, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(shortName) )
            throw new ArgumentException("Argument cannot be null or empty.", nameof(shortName));

        var chain = databaseContext.Chains.FirstOrDefault(x => x.SHORT_NAME == shortName);
        if ( chain != null )
        {
            chain.SHORT_NAME = shortName;

            if ( saveChanges ) databaseContext.SaveChanges();
        }
        else
        {
            chain = new Chain {SHORT_NAME = shortName};

            databaseContext.Chains.Add(chain);

            try
            {
                if ( saveChanges ) databaseContext.SaveChanges();
            }
            catch ( Exception ex )
            {
                var exMessage = ex.ToString();
                if ( exMessage.Contains("duplicate key value violates unique constraint") &&
                     exMessage.Contains("IX_Chains_SHORT_NAME") )
                {
                    // We tried to create same record in two threads concurrently.
                    // Now we should just remove duplicating record and get an existing record.
                    databaseContext.Chains.Remove(chain);
                    chain = databaseContext.Chains.First(x => x.SHORT_NAME == shortName);
                }
                else
                    // Unknown exception.
                    throw;
            }
        }

        return chain;
    }


    public static Task<Chain> GetAsync(ApiCacheDbContext databaseContext, string name)
    {
        return databaseContext.Chains.SingleAsync(x => x.SHORT_NAME == name);
    }

    public static Chain Get(ApiCacheDbContext databaseContext, string shortName)
    {
        if ( string.IsNullOrEmpty(shortName) )
            throw new ArgumentException("Argument cannot be null or empty.", "shortName");

        return databaseContext.Chains.FirstOrDefault(x => x.SHORT_NAME == shortName);
    }
}
