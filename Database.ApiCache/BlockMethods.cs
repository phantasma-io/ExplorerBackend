using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace Database.ApiCache;

public static class BlockMethods
{
    // Checks if table has entry with given height,
    // and adds new entry, if there's no entry available.
    public static void Upsert(ApiCacheDbContext databaseContext, string chainShortName, string height,
        long unixTimestampInSeconds, JsonDocument data, int chainId, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(chainShortName) )
            throw new ArgumentException("Argument cannot be null or empty.", "chainShortName");

        if ( string.IsNullOrEmpty(height) ) throw new ArgumentException("Argument cannot be null or empty.", "height");

        var block = databaseContext.Blocks.FirstOrDefault(x => x.ChainId == chainId && x.HEIGHT == height);

        if ( block != null ) return;

        block = new Block {ChainId = chainId, HEIGHT = height, TIMESTAMP = unixTimestampInSeconds, DATA = data};
        databaseContext.Blocks.Add(block);

        ChainMethods.SetLastProcessedBlock(databaseContext, chainId, BigInteger.Parse(height));

        if ( !saveChanges ) return;

        try
        {
            databaseContext.SaveChanges();
        }
        catch ( Exception ex )
        {
            var exMessage = ex.ToString();
            if ( exMessage.Contains("duplicate key value violates unique constraint") &&
                 exMessage.Contains("IX_Blocks_ChainId_HEIGHT") )
            {
                // We tried to create same record in two threads concurrently.
                // Now we should just remove duplicating record and get an existing record.
                databaseContext.Blocks.Remove(block);
                block = databaseContext.Blocks.First(x => x.ChainId == chainId && x.HEIGHT == height);
            }
            else
                // Unknown exception.
                throw;
        }
    }


    public static long GetTimestamp(ApiCacheDbContext databaseContext, string chainShortName, string height)
    {
        var chainId = ChainMethods.GetId(databaseContext, chainShortName);

        var block = databaseContext.Blocks.FirstOrDefault(x => x.ChainId == chainId && x.HEIGHT == height);
        return block?.TIMESTAMP ?? 0;
    }


    public static Block GetByHeight(ApiCacheDbContext databaseContext, int chainId, string height)
    {
        return databaseContext.Blocks.FirstOrDefault(x => x.ChainId == chainId && x.HEIGHT == height);
    }
}
