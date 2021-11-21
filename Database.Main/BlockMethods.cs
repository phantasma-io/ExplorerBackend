using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.Main
{
    public static class BlockMethods
    {
        // Checks if "Blocks" table has entry with given chain id and height,
        // and adds new entry, if there's no entry available.
        // Returns new or existing entry's Id.
        public static Block Upsert(MainDbContext databaseContext, int chainId, System.Numerics.BigInteger height, Int64 timestampUnixSeconds, bool saveChanges = true)
        {
            var entry = databaseContext.Blocks.Where(x => x.ChainId == chainId && x.TIMESTAMP_UNIX_SECONDS == timestampUnixSeconds && x.HEIGHT == height.ToString()).FirstOrDefault();

            /*if (entry == null)
            {
                // Checking if entry has been added already
                // but not yet inserted into database.
                entry = (Block)Utils.GetTrackedObjects(databaseContext, typeof(Block)).Where(x => ((Block)x).ChainId == chainId && ((Block)x).TIMESTAMP == timestamp && ((Block)x).HEIGHT == height.ToString()).FirstOrDefault();
            }*/

            if (entry != null)
            {
                return entry;
            }

            entry = new Block { ChainId = chainId, HEIGHT = height.ToString(), TIMESTAMP_UNIX_SECONDS = timestampUnixSeconds };

            databaseContext.Blocks.Add(entry);

            if(saveChanges)
                databaseContext.SaveChanges();

            return entry;
        }

        public static Block Get(MainDbContext databaseContext, int dbBlockId)
        {
            return databaseContext.Blocks.Where(x => x.ID == dbBlockId).FirstOrDefault();
        }
        
        public static Block GetByHeight(MainDbContext databaseContext, int chainId, BigInteger height)
        {
            return databaseContext.Blocks.Where(x => x.ChainId == chainId && x.HEIGHT == height.ToString()).FirstOrDefault();
        }
        
        public static async Task<Block> GetByHeightAsync(MainDbContext databaseContext, int chainId, BigInteger height)
        {
            return await databaseContext.Blocks.AsQueryable().Where(x => x.ChainId == chainId && x.HEIGHT == height.ToString()).FirstOrDefaultAsync();
        }
    }
}