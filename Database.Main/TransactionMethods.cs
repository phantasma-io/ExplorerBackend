using System.Linq;

namespace Database.Main;

public static class TransactionMethods
{
    // Checks if "Transactions" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static Transaction Upsert(MainDbContext databaseContext, Block block, int txIndex, string hash,
        long timestampUnixSeconds, string payload, string scriptRaw, string result, string fee, long expiration,
        bool saveChanges = true)
    {
        ContractMethods.Drop0x(ref hash);

        var entry = databaseContext.Transactions
            .FirstOrDefault(x => x.Block == block && x.HASH == hash) ?? DbHelper
            .GetTracked<Transaction>(databaseContext)
            .FirstOrDefault(x => x.Block == block && x.HASH == hash);

        if ( entry != null ) return entry;

        entry = new Transaction
        {
            Block = block,
            INDEX = txIndex,
            HASH = hash,
            TIMESTAMP_UNIX_SECONDS = timestampUnixSeconds,
            PAYLOAD = payload,
            SCRIPT_RAW = scriptRaw,
            RESULT = result,
            FEE = fee,
            EXPIRATION = expiration
        };

        databaseContext.Transactions.Add(entry);

        if ( saveChanges ) databaseContext.SaveChanges();

        return entry;
    }


    public static Transaction GetNextId(MainDbContext dbContext, int skip)
    {
        return dbContext.Transactions.OrderByDescending(x => x.ID).Skip(skip).FirstOrDefault();
    }


    public static Transaction GetById(MainDbContext dbContext, int id)
    {
        return dbContext.Transactions.FirstOrDefault(x => x.ID == id);
    }


    public static Transaction GetByHash(MainDbContext dbContext, string hash)
    {
        return dbContext.Transactions.FirstOrDefault(x => x.HASH == hash);
    }
}
