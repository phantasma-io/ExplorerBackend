using System;
using System.Linq;
using System.Numerics;

namespace Database.ApiCache;

public static class ChainMethods
{
    // Checks if "Chains" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static int Upsert(ApiCacheDbContext databaseContext, string shortName)
    {
        if ( string.IsNullOrEmpty(shortName) )
            throw new ArgumentException("Argument cannot be null or empty.", nameof(shortName));

        int chainId;
        var chain = databaseContext.Chains.FirstOrDefault(x =>
            string.Equals(x.SHORT_NAME.ToUpper(), shortName.ToUpper()));
        if ( chain != null )
        {
            chainId = chain.ID;

            chain.SHORT_NAME = shortName;

            databaseContext.SaveChanges();
        }
        else
        {
            chain = new Chain {SHORT_NAME = shortName};

            databaseContext.Chains.Add(chain);

            try
            {
                databaseContext.SaveChanges();
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
                    chain = databaseContext.Chains.First(
                        x => string.Equals(x.SHORT_NAME.ToUpper(), shortName.ToUpper()));
                }
                else
                    // Unknown exception.
                    throw;
            }

            chainId = chain.ID;
        }

        return chainId;
    }


    public static int GetId(ApiCacheDbContext databaseContext, string shortName)
    {
        if ( string.IsNullOrEmpty(shortName) )
            throw new ArgumentException("Argument cannot be null or empty.", "shortName");

        var chain = databaseContext.Chains.FirstOrDefault(x =>
            string.Equals(x.SHORT_NAME.ToUpper(), shortName.ToUpper()));
        return chain?.ID ?? 0;
    }


    public static Chain Get(ApiCacheDbContext databaseContext, int id)
    {
        return databaseContext.Chains.Single(x => x.ID == id);
    }


    public static BigInteger? GetLastProcessedBlock(ApiCacheDbContext databaseContext, int chainId)
    {
        var chain = Get(databaseContext, chainId);

        if ( chain?.CURRENT_HEIGHT == null ) return null;

        return BigInteger.Parse(Get(databaseContext, chainId).CURRENT_HEIGHT);
    }


    public static void SetLastProcessedBlock(ApiCacheDbContext databaseContext, int chainId, BigInteger height,
        bool saveChanges = true)
    {
        var chain = Get(databaseContext, chainId);
        chain.CURRENT_HEIGHT = height.ToString();

        if ( saveChanges ) databaseContext.SaveChanges();
    }
}
