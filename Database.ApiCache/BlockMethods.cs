using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.ApiCache;

public static class BlockMethods
{
    // Checks if table has entry with given height,
    // and adds new entry, if there's no entry available.
    public static Task<Block> GetByHeightAsync(ApiCacheDbContext databaseContext, string chainName, string height)
    {
        return databaseContext.Blocks.FirstOrDefaultAsync(x => x.Chain.SHORT_NAME == chainName && x.HEIGHT == height);
    }


    public static async Task UpsertAsync(ApiCacheDbContext dbContext, string height, long unixTimestampInSeconds,
        JsonDocument data, string chainName)
    {
        if ( string.IsNullOrEmpty(height) ) throw new ArgumentException("Argument cannot be null or empty.", "height");

        var chain = await ChainMethods.GetAsync(dbContext, chainName);
        var block = await dbContext.Blocks.FirstOrDefaultAsync(x => x.Chain == chain && x.HEIGHT == height);

        if ( block != null ) return;

        block = new Block {Chain = chain, HEIGHT = height, TIMESTAMP = unixTimestampInSeconds, DATA = data};
        await dbContext.Blocks.AddAsync(block);
    }
}
