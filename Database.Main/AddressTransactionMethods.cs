using System.Collections.Generic;
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

        entry = new AddressTransaction {Address = address, Transaction = transaction};
        databaseContext.AddressTransactions.Add(entry);

        if ( saveChanges ) databaseContext.SaveChanges();

        return entry;
    }


    public static void InsertIfNotExists(MainDbContext databaseContext, Address address,
        List<string> transactionHashList, bool saveChanges = true)
    {
        if ( !transactionHashList.Any() ) return;

        var addressTransactionList = ( from transactionHash in transactionHashList
            select TransactionMethods.GetByHash(databaseContext, transactionHash)
            into transaction
            where transaction != null
            let entry =
                databaseContext.AddressTransactions.FirstOrDefault(x =>
                    x.Address == address && x.Transaction == transaction)
            where entry == null
            select new AddressTransaction {Address = address, Transaction = transaction} ).ToList();

        databaseContext.AddressTransactions.AddRange(addressTransactionList);
        if ( saveChanges ) databaseContext.SaveChanges();
    }
}
