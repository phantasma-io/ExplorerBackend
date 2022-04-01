using System;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
using Transaction = GhostDevs.Service.ApiResults.Transaction;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(TransactionResult), "Returns the transaction on the backend.", false, 10)]
    public TransactionResult Transactions(
        [APIParameter("Order by [id, hash]", "string")]
        string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("hash", "string")] string hash = "",
        [APIParameter("hash (partial match)", "string")]
        string hash_partial = "",
        [APIParameter("Address", "string")] string address = "",
        [APIParameter("Date (less than)", "string")]
        string date_less = "",
        [APIParameter("Date (greater than)", "string")]
        string date_greater = "",
        [APIParameter("Block Hash", "string")] string block_hash = "",
        [APIParameter("Block Height", "string")]
        string block_height = "",
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0
    )
    {
        long totalResults = 0;
        Transaction[] transactionArray;

        using ( var databaseContext = new MainDbContext() )
        {
            try
            {
                if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                    throw new APIException("Unsupported value for 'order_by' parameter.");

                if ( !ArgValidation.CheckOrderDirection(order_direction) )
                    throw new APIException("Unsupported value for 'order_direction' parameter.");

                if ( !ArgValidation.CheckLimit(limit) )
                    throw new APIException("Unsupported value for 'limit' parameter.");

                if ( !string.IsNullOrEmpty(hash) && !ArgValidation.CheckString(hash) )
                    throw new APIException("Unsupported value for 'hash' parameter.");

                if ( !string.IsNullOrEmpty(hash_partial) && !ArgValidation.CheckAddress(hash_partial) )
                    throw new APIException("Unsupported value for 'hash_partial' parameter.");

                if ( !string.IsNullOrEmpty(address) && !ArgValidation.CheckAddress(address) )
                    throw new APIException("Unsupported value for 'address' parameter.");

                ContractMethods.Drop0x(ref address);

                if ( !string.IsNullOrEmpty(date_less) && !ArgValidation.CheckDateString(date_less) )
                    throw new APIException("Unsupported value for 'date_less' parameter.");

                if ( !string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckDateString(date_greater) )
                    throw new APIException("Unsupported value for 'date_greater' parameter.");

                if ( !string.IsNullOrEmpty(block_hash) && !ArgValidation.CheckString(block_hash) )
                    throw new APIException("Unsupported value for 'block_hash' parameter.");

                if ( !string.IsNullOrEmpty(block_height) && !ArgValidation.CheckNumber(block_height) )
                    throw new APIException("Unsupported value for 'block_height' parameter.");

                var startTime = DateTime.Now;

                var query = databaseContext.Transactions.AsQueryable();


                if ( !string.IsNullOrEmpty(hash) )
                    query = query.Where(x => string.Equals(x.HASH.ToUpper(), hash.ToUpper()));

                if ( !string.IsNullOrEmpty(hash_partial) )
                    query = query.Where(x => x.HASH.ToUpper().Contains(hash_partial.ToUpper()));

                if ( !string.IsNullOrEmpty(date_less) )
                    query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

                if ( !string.IsNullOrEmpty(date_greater) )
                    query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

                if ( !string.IsNullOrEmpty(address) )
                {
                    var addressTransactions = AddressTransactionMethods
                        .GetAddressTransactionsByAddress(databaseContext, address).ToList();
                    query = query.Where(x => x.AddressTransactions.Any(y => addressTransactions.Contains(y)));
                }

                if ( !string.IsNullOrEmpty(block_hash) )
                    query = query.Where(x => string.Equals(x.Block.HASH.ToUpper(), block_hash.ToUpper()));

                if ( !string.IsNullOrEmpty(block_height) )
                    query = query.Where(x => x.Block.HEIGHT == block_height);

                // Count total number of results before adding order and limit parts of query.
                if ( with_total == 1 )
                    totalResults = query.Count();

                //in case we add more to sort
                if ( order_direction == "asc" )
                    query = order_by switch
                    {
                        "id" => query.OrderBy(x => x.ID),
                        "hash" => query.OrderBy(x => x.HASH),
                        _ => query
                    };
                else
                    query = order_by switch
                    {
                        "id" => query.OrderByDescending(x => x.ID),
                        "hash" => query.OrderByDescending(x => x.HASH),
                        _ => query
                    };


                transactionArray = ( from x in query.Skip(offset).Take(limit)
                    let events = x.Events
                    select new Transaction
                    {
                        hash = x.HASH,
                        block_hash = x.Block.HASH,
                        block_height = x.Block.HEIGHT,
                        index = x.INDEX,
                        date = x.TIMESTAMP_UNIX_SECONDS.ToString()
                    } ).ToArray();

                var responseTime = DateTime.Now - startTime;

                Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
            }
            catch ( APIException )
            {
                throw;
            }
            catch ( Exception exception )
            {
                var logMessage = LogEx.Exception("Transaction()", exception);

                throw new APIException(logMessage, exception);
            }
        }

        return new TransactionResult
            {total_results = with_total == 1 ? totalResults : null, transactions = transactionArray};
    }
}
