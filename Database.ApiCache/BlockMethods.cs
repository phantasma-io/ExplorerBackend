using System;
using System.Linq;
using System.Text.Json;

namespace Database.ApiCache
{
    public static class BlockMethods
    {
        // Checks if table has entry with given height,
        // and adds new entry, if there's no entry available.
        public static void Upsert(ApiCacheDatabaseContext databaseContext, string chainShortName, string height, Int64 unixTimestampInSeconds, JsonDocument data, bool saveChanges = true)
        {
            if (string.IsNullOrEmpty(chainShortName))
                throw new System.ArgumentException("Argument cannot be null or empty.", "chainShortName");
            if (string.IsNullOrEmpty(height))
                throw new System.ArgumentException("Argument cannot be null or empty.", "height");

            var chainId = ChainMethods.Upsert(databaseContext, chainShortName);

            var block = databaseContext.Blocks.Where(x => x.ChainId == chainId && x.HEIGHT == height).FirstOrDefault();

            if (block == null)
            {
                block = new Block
                {
                    ChainId = chainId,
                    HEIGHT = height,
                    TIMESTAMP = unixTimestampInSeconds,
                    DATA = data
                };
                databaseContext.Blocks.Add(block);

                if (saveChanges)
                {
                    try
                    {
                        databaseContext.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        var exMessage = ex.ToString();
                        if (exMessage.Contains("duplicate key value violates unique constraint") &&
                            exMessage.Contains("IX_Blocks_ChainId_HEIGHT"))
                        {
                            // We tried to create same record in two threads concurrently.
                            // Now we should just remove duplicating record and get an existing record.
                            databaseContext.Blocks.Remove(block);
                            block = databaseContext.Blocks.Where(x => x.ChainId == chainId && x.HEIGHT == height).First();
                        }
                        else
                        {
                            // Unknown exception.
                            throw;
                        }
                    }
                }
            }
        }

        public static Int64 GetTimestamp(ApiCacheDatabaseContext databaseContext, string chainShortName, string height)
        {
            var chainId = ChainMethods.GetId(databaseContext, chainShortName);

            var block = databaseContext.Blocks.Where(x => x.ChainId == chainId && x.HEIGHT == height).FirstOrDefault();
            if (block == null)
                return 0;
            
            return block.TIMESTAMP;
        }
        
        public static Block GetByHeight(ApiCacheDatabaseContext databaseContext, int chainId, string height)
        {
            return databaseContext.Blocks.Where(x => x.ChainId == chainId && x.HEIGHT == height).FirstOrDefault();
        }
    }
}
