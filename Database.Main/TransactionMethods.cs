using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Database.Main;

public static class TransactionMethods
{
    // Checks if "Transactions" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static async Task<Transaction> UpsertAsync(MainDbContext databaseContext, Block block, int txIndex, string hash,
        ulong timestampUnixSeconds, string payload, string scriptRaw, string result, string fee, ulong expiration,
        string gasPrice, string gasLimit, string state, string sender, string gasPayer, string gasTarget,
        byte? carbonTxType = null, string carbonTxData = null,
        Address senderAddress = null, Address gasPayerAddress = null, Address gasTargetAddress = null,
        bool skipAddressTransactionExistsCheck = false, bool createAddressTransactionLinks = true,
        IDictionary<string, Transaction> existingTransactionsByHash = null,
        Queue<int> reservedTransactionIds = null,
        IList<Transaction> bufferedTransactions = null)
    {
        const string UnlimitedGasRaw = "18446744073709551615"; // TxMsg.NoMaxGas

        if (existingTransactionsByHash != null && existingTransactionsByHash.TryGetValue(hash, out var existingByHash))
            return existingByHash;

        var entry = DbHelper
            .GetTracked<Transaction>(databaseContext)
            .FirstOrDefault(x => x.Block == block && x.HASH == hash);

        if (entry != null)
        {
            existingTransactionsByHash?[hash] = entry;
            return entry;
        }

        if (existingTransactionsByHash == null)
        {
            entry = await databaseContext.Transactions
                .FirstOrDefaultAsync(x => x.Block == block && x.HASH == hash);
            if (entry != null)
                return entry;
        }

        var transactionState = TransactionStateMethods.Upsert(databaseContext, state, false);
        if (bufferedTransactions != null && transactionState.ID <= 0)
        {
            // Rare bootstrap path: persist newly created state row once so
            // set-based transaction insert can reference a stable FK id.
            await databaseContext.SaveChangesAsync();
        }

        senderAddress ??= await AddressMethods.UpsertAsync(databaseContext, block.Chain, sender);
        gasPayerAddress ??= await AddressMethods.UpsertAsync(databaseContext, block.Chain, gasPayer);
        gasTargetAddress ??= await AddressMethods.UpsertAsync(databaseContext, block.Chain, gasTarget);

        var kcalDecimals = TokenMethods.GetKcalDecimals(databaseContext, block.Chain);

        var hasUnlimitedGas = gasLimit == UnlimitedGasRaw;
        var gasLimitFormatted = hasUnlimitedGas ? null : Utils.ToDecimal(gasLimit, kcalDecimals);
        var gasPriceFormatted = Utils.ToDecimal(gasPrice, kcalDecimals);
        var feeFormatted = Utils.ToDecimal(fee, kcalDecimals);

        entry = new Transaction
        {
            Block = block,
            INDEX = txIndex,
            HASH = hash,
            TIMESTAMP_UNIX_SECONDS = (long)timestampUnixSeconds,
            PAYLOAD = payload,
            SCRIPT_RAW = scriptRaw,
            RESULT = result,
            FEE = feeFormatted,
            FEE_RAW = fee,
            EXPIRATION = (long)expiration,
            GAS_PRICE = gasPriceFormatted,
            GAS_PRICE_RAW = gasPrice,
            GAS_LIMIT = gasLimitFormatted,
            GAS_LIMIT_RAW = gasLimit,
            CARBON_TX_TYPE = carbonTxType,
            CARBON_TX_DATA = carbonTxData,
            State = transactionState,
            Sender = senderAddress,
            GasPayer = gasPayerAddress,
            GasTarget = gasTargetAddress
        };

        if (bufferedTransactions != null)
        {
            if (reservedTransactionIds == null || reservedTransactionIds.Count == 0)
                throw new System.InvalidOperationException(
                    "Buffered transaction insert requires pre-reserved transaction IDs.");

            entry.ID = reservedTransactionIds.Dequeue();
            bufferedTransactions.Add(entry);
        }
        else
        {
            await databaseContext.Transactions.AddAsync(entry);
        }

        existingTransactionsByHash?[hash] = entry;

        if (createAddressTransactionLinks)
        {
            await AddressTransactionMethods.UpsertAsync(databaseContext, senderAddress, entry,
                !skipAddressTransactionExistsCheck);
            if (gasPayerAddress.ADDRESS != senderAddress.ADDRESS)
            {
                await AddressTransactionMethods.UpsertAsync(databaseContext, gasPayerAddress, entry,
                    !skipAddressTransactionExistsCheck);
            }
            if (gasTargetAddress.ADDRESS != gasPayerAddress.ADDRESS)
            {
                await AddressTransactionMethods.UpsertAsync(databaseContext, gasTargetAddress, entry,
                    !skipAddressTransactionExistsCheck);
            }
        }

        return entry;
    }

    public static async Task<Queue<int>> ReserveIdsAsync(NpgsqlConnection dbConnection,
        NpgsqlTransaction dbTransaction, int rowCount)
    {
        var ids = new Queue<int>();
        if (rowCount <= 0)
            return ids;

        await using var reserveIdsCmd = new NpgsqlCommand(@"
SELECT nextval(pg_get_serial_sequence('""Transactions""', 'ID'))::integer
FROM generate_series(1, @row_count);
", dbConnection, dbTransaction);

        reserveIdsCmd.Parameters.Add("@row_count", NpgsqlDbType.Integer).Value = rowCount;

        await using var reader = await reserveIdsCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            ids.Enqueue(reader.GetInt32(0));

        if (ids.Count != rowCount)
            throw new System.InvalidOperationException(
                $"Failed to reserve transaction IDs for batch insert: expected {rowCount}, got {ids.Count}.");

        return ids;
    }

    // Transaction ingest is now buffered per block and flushed set-based,
    // so hot sync path avoids EF per-entity INSERT overhead.
    public static async Task InsertBatchAsync(NpgsqlConnection dbConnection, NpgsqlTransaction dbTransaction,
        IReadOnlyList<Transaction> transactions)
    {
        if (transactions == null || transactions.Count == 0)
            return;

        var count = transactions.Count;
        var ids = new int[count];
        var indexes = new int[count];
        var blockIds = new int[count];
        var timestamps = new long[count];
        var payloads = new string[count];
        var scriptRaw = new string[count];
        var results = new string[count];
        var fee = new string[count];
        var feeRaw = new string[count];
        var expiration = new long[count];
        var stateIds = new int[count];
        var gasPrice = new string[count];
        var gasPriceRaw = new string[count];
        var gasLimit = new string[count];
        var gasLimitRaw = new string[count];
        var carbonTxType = new short?[count];
        var carbonTxData = new string[count];
        var senderIds = new int[count];
        var gasPayerIds = new int[count];
        var gasTargetIds = new int[count];
        var hashes = new string[count];

        for (var i = 0; i < count; i++)
        {
            var tx = transactions[i];
            if (tx.ID <= 0)
                throw new System.InvalidOperationException("Cannot batch-insert transaction without reserved ID.");

            ids[i] = tx.ID;
            indexes[i] = tx.INDEX;
            blockIds[i] = tx.BlockId > 0 ? tx.BlockId : tx.Block?.ID ?? 0;
            timestamps[i] = tx.TIMESTAMP_UNIX_SECONDS;
            payloads[i] = tx.PAYLOAD;
            scriptRaw[i] = tx.SCRIPT_RAW;
            results[i] = tx.RESULT;
            fee[i] = tx.FEE;
            feeRaw[i] = tx.FEE_RAW;
            expiration[i] = tx.EXPIRATION;
            stateIds[i] = tx.StateId > 0 ? tx.StateId : tx.State?.ID ?? 0;
            gasPrice[i] = tx.GAS_PRICE;
            gasPriceRaw[i] = tx.GAS_PRICE_RAW;
            gasLimit[i] = tx.GAS_LIMIT;
            gasLimitRaw[i] = tx.GAS_LIMIT_RAW;
            carbonTxType[i] = tx.CARBON_TX_TYPE.HasValue ? (short?)tx.CARBON_TX_TYPE.Value : null;
            carbonTxData[i] = tx.CARBON_TX_DATA;
            senderIds[i] = tx.SenderId > 0 ? tx.SenderId : tx.Sender?.ID ?? 0;
            gasPayerIds[i] = tx.GasPayerId > 0 ? tx.GasPayerId : tx.GasPayer?.ID ?? 0;
            gasTargetIds[i] = tx.GasTargetId > 0 ? tx.GasTargetId : tx.GasTarget?.ID ?? 0;
            hashes[i] = tx.HASH;

            if (blockIds[i] <= 0 || stateIds[i] <= 0 || senderIds[i] <= 0 || gasPayerIds[i] <= 0 || gasTargetIds[i] <= 0)
                throw new System.InvalidOperationException(
                    $"Cannot batch-insert transaction {tx.HASH}: unresolved FK ids.");

            tx.BlockId = blockIds[i];
            tx.StateId = stateIds[i];
            tx.SenderId = senderIds[i];
            tx.GasPayerId = gasPayerIds[i];
            tx.GasTargetId = gasTargetIds[i];
        }

        await using var cmd = new NpgsqlCommand(@"
INSERT INTO ""Transactions"" (
    ""ID"",
    ""INDEX"",
    ""BlockId"",
    ""TIMESTAMP_UNIX_SECONDS"",
    ""PAYLOAD"",
    ""SCRIPT_RAW"",
    ""RESULT"",
    ""FEE"",
    ""FEE_RAW"",
    ""EXPIRATION"",
    ""StateId"",
    ""GAS_PRICE"",
    ""GAS_PRICE_RAW"",
    ""GAS_LIMIT"",
    ""GAS_LIMIT_RAW"",
    ""CARBON_TX_TYPE"",
    ""CARBON_TX_DATA"",
    ""SenderId"",
    ""GasPayerId"",
    ""GasTargetId"",
    ""HASH""
)
SELECT
    row.""ID"",
    row.""INDEX"",
    row.""BlockId"",
    row.""TIMESTAMP_UNIX_SECONDS"",
    row.""PAYLOAD"",
    row.""SCRIPT_RAW"",
    row.""RESULT"",
    row.""FEE"",
    row.""FEE_RAW"",
    row.""EXPIRATION"",
    row.""StateId"",
    row.""GAS_PRICE"",
    row.""GAS_PRICE_RAW"",
    row.""GAS_LIMIT"",
    row.""GAS_LIMIT_RAW"",
    row.""CARBON_TX_TYPE"",
    row.""CARBON_TX_DATA"",
    row.""SenderId"",
    row.""GasPayerId"",
    row.""GasTargetId"",
    row.""HASH""
FROM UNNEST(
    @ids,
    @indexes,
    @block_ids,
    @timestamps,
    @payloads,
    @script_raw,
    @results,
    @fee,
    @fee_raw,
    @expiration,
    @state_ids,
    @gas_price,
    @gas_price_raw,
    @gas_limit,
    @gas_limit_raw,
    @carbon_tx_type,
    @carbon_tx_data,
    @sender_ids,
    @gas_payer_ids,
    @gas_target_ids,
    @hashes
) AS row(
    ""ID"",
    ""INDEX"",
    ""BlockId"",
    ""TIMESTAMP_UNIX_SECONDS"",
    ""PAYLOAD"",
    ""SCRIPT_RAW"",
    ""RESULT"",
    ""FEE"",
    ""FEE_RAW"",
    ""EXPIRATION"",
    ""StateId"",
    ""GAS_PRICE"",
    ""GAS_PRICE_RAW"",
    ""GAS_LIMIT"",
    ""GAS_LIMIT_RAW"",
    ""CARBON_TX_TYPE"",
    ""CARBON_TX_DATA"",
    ""SenderId"",
    ""GasPayerId"",
    ""GasTargetId"",
    ""HASH""
);
", dbConnection, dbTransaction);

        cmd.Parameters.Add("@ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = ids;
        cmd.Parameters.Add("@indexes", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = indexes;
        cmd.Parameters.Add("@block_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = blockIds;
        cmd.Parameters.Add("@timestamps", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = timestamps;
        cmd.Parameters.Add("@payloads", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = payloads;
        cmd.Parameters.Add("@script_raw", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = scriptRaw;
        cmd.Parameters.Add("@results", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = results;
        cmd.Parameters.Add("@fee", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = fee;
        cmd.Parameters.Add("@fee_raw", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = feeRaw;
        cmd.Parameters.Add("@expiration", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = expiration;
        cmd.Parameters.Add("@state_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = stateIds;
        cmd.Parameters.Add("@gas_price", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = gasPrice;
        cmd.Parameters.Add("@gas_price_raw", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = gasPriceRaw;
        cmd.Parameters.Add("@gas_limit", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = gasLimit;
        cmd.Parameters.Add("@gas_limit_raw", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = gasLimitRaw;
        cmd.Parameters.Add("@carbon_tx_type", NpgsqlDbType.Array | NpgsqlDbType.Smallint).Value = carbonTxType;
        cmd.Parameters.Add("@carbon_tx_data", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = carbonTxData;
        cmd.Parameters.Add("@sender_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = senderIds;
        cmd.Parameters.Add("@gas_payer_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = gasPayerIds;
        cmd.Parameters.Add("@gas_target_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = gasTargetIds;
        cmd.Parameters.Add("@hashes", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = hashes;

        await cmd.ExecuteNonQueryAsync();
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
