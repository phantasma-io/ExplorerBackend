using System.Collections.Generic;
using System.Linq;

namespace Database.Main;

public static class AddressTransactionMethods
{
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


    public static IEnumerable<AddressTransaction> GetAddressTransactionsByAddress(MainDbContext databaseContext,
        string address, bool isValidAddress = true)
    {
        return string.IsNullOrEmpty(address)
            ? null
            : isValidAddress ? databaseContext.AddressTransactions.Where(x => x.Address.ADDRESS == address)
            : databaseContext.AddressTransactions.Where(x => x.Address.USER_NAME == address || x.Address.ADDRESS_NAME == address);
    }
}
