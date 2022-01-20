using System.Linq;

namespace Database.Main;

public static class TransactionMethods
{
    public static string Prepend0x(string address, string chainShortName = null)
    {
        if ( string.IsNullOrEmpty(address) ) return address;

        // return "0x" + address;

        return address;
    }


    // Checks if "Transactions" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static Transaction Upsert(MainDbContext databaseContext, Block block, int txIndex, string hash,
        bool saveChanges = true)
    {
        ContractMethods.Drop0x(ref hash);

        var entry = databaseContext.Transactions
            .FirstOrDefault(x => x.Block == block && x.HASH == hash);

        if ( entry == null )
            // Checking if entry has been added already
            // but not yet inserted into database.
            entry = DbHelper.GetTracked<Transaction>(databaseContext)
                .FirstOrDefault(x => x.Block == block && x.HASH == hash);

        if ( entry != null ) return entry;

        entry = new Transaction {Block = block, INDEX = txIndex, HASH = hash};

        databaseContext.Transactions.Add(entry);

        if ( saveChanges ) databaseContext.SaveChanges();

        return entry;
    }
}
