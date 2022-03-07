using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Database.Main;

public static class ChainMethods
{
    // Checks if "Chains" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static int Upsert(MainDbContext databaseContext, string name)
    {
        int chainId;
        var chain = databaseContext.Chains.FirstOrDefault(x => string.Equals(x.NAME.ToUpper(), name.ToUpper()));
        if ( chain != null )
            chainId = chain.ID;
        else
        {
            chain = new Chain {NAME = name, CURRENT_HEIGHT = "0"};

            databaseContext.Chains.Add(chain);
            databaseContext.SaveChanges();

            chainId = chain.ID;
        }

        return chainId;
    }


    public static Chain Get(MainDbContext databaseContext, int id)
    {
        return databaseContext.Chains.Single(x => x.ID == id);
    }


    public static Chain Get(MainDbContext databaseContext, string name)
    {
        return databaseContext.Chains.Single(x => string.Equals(x.NAME.ToUpper(), name.ToUpper()));
    }


    public static int GetId(MainDbContext databaseContext, string name)
    {
        if ( !string.IsNullOrEmpty(name) )
            return databaseContext.Chains.Where(x => string.Equals(x.NAME.ToUpper(), name.ToUpper())).Select(x => x.ID)
                .FirstOrDefault();

        return 0;
    }


    public static BigInteger GetLastProcessedBlock(MainDbContext databaseContext, int chainId)
    {
        return BigInteger.Parse(Get(databaseContext, chainId).CURRENT_HEIGHT);
    }


    public static void SetLastProcessedBlock(MainDbContext databaseContext, int chainId, BigInteger height,
        bool saveChanges = true)
    {
        var chain = Get(databaseContext, chainId);
        chain.CURRENT_HEIGHT = height.ToString();

        if ( saveChanges ) databaseContext.SaveChanges();
    }


    public static IEnumerable<Chain> GetChains(MainDbContext dbContext)
    {
        return dbContext.Chains.ToList();
    }


    public static List<int> GetChainsIds(MainDbContext dbContext)
    {
        var chainList = GetChains(dbContext);
        return chainList.Select(chain => chain.ID).ToList();
    }


    public static List<string> GetChainNames(MainDbContext dbContext)
    {
        return GetChains(dbContext).Select(chain => chain.NAME).ToList();
    }
}
