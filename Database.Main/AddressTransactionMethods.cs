using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Database.Main;

public static class AddressTransactionMethods
{
    // Keep the earliest transaction timestamp per address as a monotonic minimum.
    // This must also initialize pre-existing addresses when their first AddressTransaction
    // link appears later (for example after legacy linkage backfills/recovery).
    public static void UpdateFirstTxUnixSeconds(Address address, long transactionTimestampUnixSeconds)
    {
        if (address == null)
            return;

        if (address.FIRST_TX_UNIX_SECONDS.HasValue)
        {
            if (address.FIRST_TX_UNIX_SECONDS.Value > transactionTimestampUnixSeconds)
            {
                address.FIRST_TX_UNIX_SECONDS = transactionTimestampUnixSeconds;
            }
            return;
        }

        address.FIRST_TX_UNIX_SECONDS = transactionTimestampUnixSeconds;
    }

    public static async Task UpsertAsync(MainDbContext databaseContext, Address address,
        Transaction transaction, bool checkDatabaseForExistingLink = true)
    {
        UpdateFirstTxUnixSeconds(address, transaction.TIMESTAMP_UNIX_SECONDS);

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

    // Block ingest can produce many Address-Transaction links in one batch.
    // Writing them set-based with ON CONFLICT avoids per-row existence probes while
    // preserving idempotency through the unique index.
    public static async Task InsertBatchAsync(NpgsqlConnection dbConnection, NpgsqlTransaction dbTransaction,
        IReadOnlyCollection<(int AddressId, int TransactionId)> links)
    {
        if (links == null || links.Count == 0)
            return;

        var addressIds = new int[links.Count];
        var transactionIds = new int[links.Count];
        var index = 0;

        foreach (var (addressId, transactionId) in links)
        {
            if (addressId <= 0 || transactionId <= 0)
                continue;

            addressIds[index] = addressId;
            transactionIds[index] = transactionId;
            index++;
        }

        if (index == 0)
            return;

        if (index != links.Count)
        {
            System.Array.Resize(ref addressIds, index);
            System.Array.Resize(ref transactionIds, index);
        }

        await using var cmd = new NpgsqlCommand(@"
INSERT INTO ""AddressTransactions"" (""AddressId"", ""TransactionId"")
SELECT link.""AddressId"", link.""TransactionId""
FROM UNNEST(@address_ids, @transaction_ids) AS link(""AddressId"", ""TransactionId"")
ON CONFLICT (""AddressId"", ""TransactionId"") DO NOTHING;
", dbConnection, dbTransaction);

        cmd.Parameters.Add("@address_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = addressIds;
        cmd.Parameters.Add("@transaction_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = transactionIds;

        await cmd.ExecuteNonQueryAsync();
    }
}
