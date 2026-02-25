using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class AddressTransactionMethods
{
    // Keep the earliest transaction timestamp per address as a monotonic minimum.
    // This must also initialize pre-existing addresses when their first AddressTransaction
    // link appears later (for example after legacy linkage backfills/recovery).
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

        address.FIRST_TX_UNIX_SECONDS = transaction.TIMESTAMP_UNIX_SECONDS;
    }

    public static async Task UpsertAsync(MainDbContext databaseContext, Address address,
        Transaction transaction, bool checkDatabaseForExistingLink = true)
    {
        UpdateFirstTxUnixSeconds(databaseContext, address, transaction);

        var addressId = address.ID;
        var transactionId = transaction.ID;

        if (checkDatabaseForExistingLink && addressId > 0 && transactionId > 0 &&
            await databaseContext.AddressTransactions
                .AnyAsync(x => x.AddressId == addressId && x.TransactionId == transactionId))
        {
            return;
        }

        // Checking if entry has been added already
        // but not yet inserted into database.
        if (DbHelper.GetTracked<AddressTransaction>(databaseContext)
            .Any(x =>
                (x.Address == address && x.Transaction == transaction) ||
                (addressId > 0 && transactionId > 0 && x.AddressId == addressId && x.TransactionId == transactionId)))
        {
            return;
        }

        var entry = new AddressTransaction { Address = address, Transaction = transaction };
        databaseContext.AddressTransactions.Add(entry);
    }
}
