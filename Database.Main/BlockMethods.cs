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
    public static async Task<Block> UpsertAsync(MainDbContext databaseContext, Chain chain, BigInteger height, long timestampUnixSeconds,
        string hash, string previousHash, uint protocol, string chainAddress, string validatorAddress, string reward)
    {
        var entry = await databaseContext.Blocks.FirstOrDefaultAsync(x =>
            x.Chain == chain && x.TIMESTAMP_UNIX_SECONDS == timestampUnixSeconds && x.HEIGHT == height.ToString());

        /*if (entry == null)
        {
            // Checking if entry has been added already
            // but not yet inserted into database.
            entry = (Block)Utils.GetTrackedObjects(databaseContext, typeof(Block)).Where(x => ((Block)x).ChainId == chainId && ((Block)x).TIMESTAMP == timestamp && ((Block)x).HEIGHT == height.ToString()).FirstOrDefault();
        }*/

        if ( entry != null ) return entry;

        var chainAddressEntry = await AddressMethods.UpsertAsync(databaseContext, chain, chainAddress);
        var validatorAddressEntry = await AddressMethods.UpsertAsync(databaseContext, chain, validatorAddress);


        entry = new Block
        {
            Chain = chain,
            HEIGHT = height.ToString(),
            TIMESTAMP_UNIX_SECONDS = timestampUnixSeconds,
            HASH = hash,
            PREVIOUS_HASH = previousHash,
            REWARD = reward,
            PROTOCOL = (int)protocol,
            ChainAddress = chainAddressEntry,
            ValidatorAddress = validatorAddressEntry
        };

        await databaseContext.Blocks.AddAsync(entry);

        return entry;
    }

    public static Block Get(MainDbContext databaseContext, int dbBlockId)
    {
        return databaseContext.Blocks.FirstOrDefault(x => x.ID == dbBlockId);
    }

    public static Block GetHighestBlock(MainDbContext dbContext, int chainId)
    {
        var id = dbContext.Blocks.Where(x => x.ChainId == chainId).Max(x => ( int? ) x.ID);
        return id != null ? Get(dbContext, ( int ) id) : null;
    }
}
