using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class AddressTransactionMethods
{
    // Keep the earliest transaction timestamp per address as a monotonic minimum.
    // Without historical backfill, we only initialize this for addresses created in current sync.
    private static void UpdateFirstTxUnixSeconds(MainDbContext databaseContext, Address address,
        Transaction transaction)
    {
        if (address == null || transaction == null)
            return;

        if (address.FIRST_TX_UNIX_SECONDS.HasValue)
        {
            if (address.FIRST_TX_UNIX_SECONDS.Value > transaction.TIMESTAMP_UNIX_SECONDS)
            {
                address.FIRST_TX_UNIX_SECONDS = transaction.TIMESTAMP_UNIX_SECONDS;
            }
            return;
        }

        if (databaseContext.Entry(address).State == EntityState.Added)
        {
            address.FIRST_TX_UNIX_SECONDS = transaction.TIMESTAMP_UNIX_SECONDS;
        }
    }

    public static async Task UpsertAsync(MainDbContext databaseContext, Address address,
        Transaction transaction)
    {
        UpdateFirstTxUnixSeconds(databaseContext, address, transaction);

        if (databaseContext.AddressTransactions
            .Any(x => x.Address == address && x.Transaction == transaction))
        {
            return;
        }

        // Checking if entry has been added already
        // but not yet inserted into database.
        if (DbHelper.GetTracked<AddressTransaction>(databaseContext)
            .Any(x => x.Address == address && x.Transaction == transaction))
        {
            return;
        }

        var entry = new AddressTransaction { Address = address, Transaction = transaction };
        databaseContext.AddressTransactions.Add(entry);
    }
}
