using System.Linq;
using System.Threading.Tasks;

namespace Database.Main;

public static class AddressTransactionMethods
{
    public static async Task UpsertAsync(MainDbContext databaseContext, Address address,
        Transaction transaction)
    {
        if ( databaseContext.AddressTransactions
            .Any(x => x.Address == address && x.Transaction == transaction) )
        {
            return;
        }

        // Checking if entry has been added already
        // but not yet inserted into database.
        if ( DbHelper.GetTracked<AddressTransaction>(databaseContext)
            .Any(x => x.Address == address && x.Transaction == transaction) )
        {
            return;
        }
        
        var entry = new AddressTransaction {Address = address, Transaction = transaction};
        databaseContext.AddressTransactions.Add(entry);
    }
}
