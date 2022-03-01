using System.Linq;

namespace Database.Main;

public static class AddressTransactionMethods
{
    public static AddressTransaction Upsert(MainDbContext databaseContext, Address address, string transactionHash,
        bool saveChanges = true)
    {
        if ( string.IsNullOrEmpty(transactionHash) ) return null;

        var transaction = TransactionMethods.GetByHash(databaseContext, transactionHash);

        //transaction not in db yet, we will get it in when we have processed the block with the transaction
        if ( transaction == null ) return null;

        var entry = databaseContext.AddressTransactions.FirstOrDefault(x =>
            x.Address == address && x.Transaction == transaction);

        if ( entry != null ) return entry;

        entry = new AddressTransaction
        {
            Address = address,
            Transaction = transaction
        };
        databaseContext.AddressTransactions.Add(entry);

        if ( saveChanges ) databaseContext.SaveChanges();

        return entry;
    }
}
