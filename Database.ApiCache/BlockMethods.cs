using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace Database.ApiCache;

public static class BlockMethods
{
    // Checks if table has entry with given height,
    // and adds new entry, if there's no entry available.
    public static Block GetByHeight(ApiCacheDbContext databaseContext, int chainId, string height)
    {
        return databaseContext.Blocks.FirstOrDefault(x => x.ChainId == chainId && x.HEIGHT == height);
    }


    public static Block Upsert(ApiCacheDbContext databaseContext, string height, long unixTimestampInSeconds,
        JsonDocument data, Chain chain, bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(height) ) throw new ArgumentException("Argument cannot be null or empty.", "height");

        var block = databaseContext.Blocks.FirstOrDefault(x => x.Chain == chain && x.HEIGHT == height);

        if ( block != null ) return null;

        block = new Block {Chain = chain, HEIGHT = height, TIMESTAMP = unixTimestampInSeconds, DATA = data};
        databaseContext.Blocks.Add(block);

        ChainMethods.SetLastProcessedBlock(databaseContext, chain, BigInteger.Parse(height));

        if ( !saveChanges ) return null;

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
                block = databaseContext.Blocks.First(x => x.Chain == chain && x.HEIGHT == height);
            }
            else
                // Unknown exception.
                throw;
        }

        return block;
    }
}
