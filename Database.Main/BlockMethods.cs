using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class BlockMethods
{
    // Checks if "Blocks" table has entry with given chain id and height,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static Block Upsert(MainDbContext databaseContext, int chainId, BigInteger height, long timestampUnixSeconds,
        string hash, string previousHash, int protocol, string chainAddress, string validatorAddress, string reward,
        bool saveChanges = true)
    {
        var entry = databaseContext.Blocks
            .FirstOrDefault(x => x.ChainId == chainId && x.TIMESTAMP_UNIX_SECONDS == timestampUnixSeconds &&
                                 x.HEIGHT == height.ToString());

        /*if (entry == null)
        {
            // Checking if entry has been added already
            // but not yet inserted into database.
            entry = (Block)Utils.GetTrackedObjects(databaseContext, typeof(Block)).Where(x => ((Block)x).ChainId == chainId && ((Block)x).TIMESTAMP == timestamp && ((Block)x).HEIGHT == height.ToString()).FirstOrDefault();
        }*/

        if ( entry != null ) return entry;

        var chainAddressEntry = AddressMethods.Upsert(databaseContext, chainId, chainAddress, saveChanges);
        var validatorAddressEntry = AddressMethods.Upsert(databaseContext, chainId, validatorAddress, saveChanges);


        entry = new Block
        {
            ChainId = chainId,
            HEIGHT = height.ToString(),
            TIMESTAMP_UNIX_SECONDS = timestampUnixSeconds,
            HASH = hash,
            PREVIOUS_HASH = previousHash,
            REWARD = reward,
            PROTOCOL = protocol,
            ChainAddress = chainAddressEntry,
            ValidatorAddress = validatorAddressEntry
        };

        databaseContext.Blocks.Add(entry);

        if ( saveChanges ) databaseContext.SaveChanges();

        return entry;
    }


    public static Block Get(MainDbContext databaseContext, int dbBlockId)
    {
        return databaseContext.Blocks.FirstOrDefault(x => x.ID == dbBlockId);
    }


    public static Block GetByHeight(MainDbContext databaseContext, int chainId, BigInteger height)
    {
        return databaseContext.Blocks
            .FirstOrDefault(x => x.ChainId == chainId && x.HEIGHT == height.ToString());
    }


    public static async Task<Block> GetByHeightAsync(MainDbContext databaseContext, int chainId, BigInteger height)
    {
        return await databaseContext.Blocks.AsQueryable()
            .Where(x => x.ChainId == chainId && x.HEIGHT == height.ToString()).FirstOrDefaultAsync();
    }


    public static Block GetHighestBlock(MainDbContext dbContext, int chainId)
    {
        var height = dbContext.Blocks.Where(x => x.ChainId == chainId).Max(x => x.HEIGHT);
        return !string.IsNullOrEmpty(height) ? GetByHeight(dbContext, chainId, BigInteger.Parse(height)) : null;
    }
}
