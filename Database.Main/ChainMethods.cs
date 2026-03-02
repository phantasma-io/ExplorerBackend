using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class ChainMethods
{
    private static long ToLongHeight(BigInteger height)
    {
        if (height > long.MaxValue || height < long.MinValue)
            throw new($"Block height {height} is out of range for bigint storage.");

        return (long)height;
    }

    // Checks if "Chains" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static Chain Upsert(MainDbContext databaseContext, string name)
    {
        var chain = databaseContext.Chains.FirstOrDefault(x => x.NAME == name);

        if (chain != null) return chain;

        chain = new Chain { NAME = name, CURRENT_HEIGHT = 0 };

        databaseContext.Chains.Add(chain);

        return chain;
    }


    public static Chain Get(MainDbContext databaseContext, int id)
    {
        return databaseContext.Chains.Single(x => x.ID == id);
    }


    public static Chain Get(MainDbContext databaseContext, string name)
    {
        return databaseContext.Chains.Single(x => x.NAME == name);
    }

    public static async Task<Chain> GetAsync(MainDbContext databaseContext, string name)
    {
        return await databaseContext.Chains.SingleAsync(x => x.NAME == name);
    }


    public static int GetId(MainDbContext databaseContext, string name)
    {
        return !string.IsNullOrEmpty(name)
            ? databaseContext.Chains.Where(x => x.NAME == name).Select(x => x.ID).FirstOrDefault()
            : 0;
    }


    public static BigInteger GetLastProcessedBlock(MainDbContext databaseContext, string chainName)
    {
        return new BigInteger(Get(databaseContext, chainName).CURRENT_HEIGHT);
    }


    public static void SetLastProcessedBlock(MainDbContext databaseContext, string chainName, BigInteger height,
        bool saveChanges = true)
    {
        var chain = Get(databaseContext, chainName);
        chain.CURRENT_HEIGHT = ToLongHeight(height);

        if (saveChanges) databaseContext.SaveChanges();
    }


    public static IEnumerable<Chain> GetChains(MainDbContext dbContext)
    {
        return dbContext.Chains.ToList();
    }

    public static List<string> GetChainNames(MainDbContext dbContext)
    {
        return GetChains(dbContext).Select(chain => chain.NAME).ToList();
    }
}
