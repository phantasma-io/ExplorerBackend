using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.ApiCache;

public static class ChainMethods
{
    // Checks if "Chains" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static Chain Upsert(ApiCacheDbContext databaseContext, string shortName)
    {
        if ( string.IsNullOrEmpty(shortName) )
            throw new ArgumentException("Argument cannot be null or empty.", nameof(shortName));

        var chain = databaseContext.Chains.FirstOrDefault(x => x.SHORT_NAME == shortName);
        if ( chain != null )
        {
            chain.SHORT_NAME = shortName;
        }
        else
        {
            chain = new Chain {SHORT_NAME = shortName};

            databaseContext.Chains.Add(chain);
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
