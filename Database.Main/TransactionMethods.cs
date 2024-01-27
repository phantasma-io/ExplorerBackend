using System.Linq;
using System.Threading.Tasks;
using Backend.Commons;
using Microsoft.EntityFrameworkCore;

namespace Database.Main;

public static class TransactionMethods
{
    // Checks if "Transactions" table has entry with given name,
    // and adds new entry, if there's no entry available.
    // Returns new or existing entry's Id.
    public static async Task<Transaction> UpsertAsync(MainDbContext databaseContext, Block block, int txIndex, string hash,
        long timestampUnixSeconds, string payload, string scriptRaw, string result, string fee, long expiration,
        string gasPrice, string gasLimit, string state, string sender, string gasPayer, string gasTarget)
    {
        var entry = await databaseContext.Transactions
            .FirstOrDefaultAsync(x => x.Block == block && x.HASH == hash) ?? DbHelper
            .GetTracked<Transaction>(databaseContext)
            .FirstOrDefault(x => x.Block == block && x.HASH == hash);

        if ( entry != null ) return entry;

        var transactionState = TransactionStateMethods.Upsert(databaseContext, state, false);
        var senderAddress = await AddressMethods.UpsertAsync(databaseContext, block.Chain, sender);
        var gasPayerAddress = await AddressMethods.UpsertAsync(databaseContext, block.Chain, gasPayer);
        var gasTargetAddress = await AddressMethods.UpsertAsync(databaseContext, block.Chain, gasTarget);

        var kcalDecimals = TokenMethods.GetKcalDecimals(databaseContext, block.Chain);
        entry = new Transaction
        {
            Block = block,
            INDEX = txIndex,
            HASH = hash,
            TIMESTAMP_UNIX_SECONDS = timestampUnixSeconds,
            PAYLOAD = payload,
            SCRIPT_RAW = scriptRaw,
            RESULT = result,
            FEE = Utils.ToDecimal(fee, kcalDecimals),
            FEE_RAW = fee,
            EXPIRATION = expiration,
            GAS_PRICE = Utils.ToDecimal(gasPrice, kcalDecimals),
            GAS_PRICE_RAW = gasPrice,
            GAS_LIMIT = Utils.ToDecimal(gasLimit, kcalDecimals),
            GAS_LIMIT_RAW = gasLimit,
            State = transactionState,
            Sender = senderAddress,
            GasPayer = gasPayerAddress,
            GasTarget = gasTargetAddress
        };

        await databaseContext.Transactions.AddAsync(entry);

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
