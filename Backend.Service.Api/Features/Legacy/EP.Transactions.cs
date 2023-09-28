using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public partial class Endpoints
{
    [ProducesResponseType(typeof(TransactionResult), ( int )HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(TransactionResult), "Returns the transaction on the backend.", false, 60, cacheTag: "transactions")]
    public static TransactionResult Transactions(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string hash = "",
        string hash_partial = "",
        string address = "",
        string date_less = "",
        string date_greater = "",
        string block_hash = "",
        string block_height = "",
        string chain = "main",
        int with_nft = 0,
        int with_events = 0,
        int with_event_data = 0,
        int with_fiat = 0,
        int with_script = 0,
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        Transaction[] transactions = null;

        const string fiatCurrency = "USD";
        var filter = !string.IsNullOrEmpty(hash) || !string.IsNullOrEmpty(hash_partial) ||
                     !string.IsNullOrEmpty(address) || !string.IsNullOrEmpty(date_less) ||
                     !string.IsNullOrEmpty(date_greater)
                     || !string.IsNullOrEmpty(block_hash) || !string.IsNullOrEmpty(block_height);

        try
        {
            #region ArgValidation

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, filter) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(hash) && !ArgValidation.CheckHash(hash.ToUpper()) )
                throw new ApiParameterException("Unsupported value for 'hash' parameter.");

            if ( !string.IsNullOrEmpty(hash_partial) && !ArgValidation.CheckHash(hash_partial.ToUpper()) )
                throw new ApiParameterException("Unsupported value for 'hash_partial' parameter.");

            if ( !string.IsNullOrEmpty(address) && !ArgValidation.CheckAddress(address) )
                throw new ApiParameterException("Unsupported value for 'address' parameter.");

            if ( !string.IsNullOrEmpty(date_less) && !ArgValidation.CheckNumber(date_less) )
                throw new ApiParameterException("Unsupported value for 'date_less' parameter.");

            if ( !string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckNumber(date_greater) )
                throw new ApiParameterException("Unsupported value for 'date_greater' parameter.");

            if ( !string.IsNullOrEmpty(block_hash) && !ArgValidation.CheckHash(block_hash.ToUpper()) )
                throw new ApiParameterException("Unsupported value for 'block_hash' parameter.");

            if ( !string.IsNullOrEmpty(block_height) && !ArgValidation.CheckNumber(block_height) )
                throw new ApiParameterException("Unsupported value for 'block_height' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            #endregion

            var startTime = DateTime.Now;
            using MainDbContext databaseContext = new();
            var fiatPricesInUsd = FiatExchangeRateMethods.GetPrices(databaseContext);

            var query = databaseContext.Transactions.AsQueryable().AsNoTracking();

            #region Filtering

            if ( !string.IsNullOrEmpty(hash) )
                query = query.Where(x => x.HASH == hash.ToUpper());

            if ( !string.IsNullOrEmpty(hash_partial) )
                query = query.Where(x => x.HASH.Contains(hash_partial.ToUpper()));

            if ( !string.IsNullOrEmpty(date_less) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

            if ( !string.IsNullOrEmpty(date_greater) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

            if ( !string.IsNullOrEmpty(address) )
            {
                bool isValidAddress = Phantasma.Core.Cryptography.Address.IsValidAddress(address);
                var addressTransactions = AddressTransactionMethods
                    .GetAddressTransactionsByAddress(databaseContext, address, isValidAddress).ToList();

                query = query.Where(x => x.AddressTransactions.Any(y => addressTransactions.Contains(y)));
            }

            if ( !string.IsNullOrEmpty(block_hash) )
                query = query.Where(x => x.Block.HASH == block_hash.ToUpper());

            if ( !string.IsNullOrEmpty(block_height) )
                query = query.Where(x => x.Block.HEIGHT == block_height);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Block.Chain.NAME == chain);

            #endregion

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

            if ( !String.IsNullOrEmpty(hash) )
            {
                query = query.Take(1);
            }else if ( limit > 0 ) query = query.Skip(offset).Take(limit);

            transactions = ProcessAllTransactions(databaseContext, query, with_script, with_events, with_event_data, with_nft, with_fiat,
                fiatCurrency, fiatPricesInUsd).GetAwaiter().GetResult();
            /*query.Select(_transaction => ProcessTransaction())*/
            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Transaction()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new TransactionResult
            { total_results = with_total == 1 ? totalResults : null, transactions = transactions };
    }
}
