using System;
using System.Linq;
using System.Net;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public partial class Endpoints
{
    [ProducesResponseType(typeof(ContractMethodHistoryResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(ContractMethodHistoryResult), "Returns the contract methods on the backend.", false, 10)]
    public static ContractMethodHistoryResult ContractMethodHistories(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string symbol = "",
        string hash = "",
        string chain = "main",
        string date_less = "",
        string date_greater = "",
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        ContractMethodHistory[] contractMethodHistoryArray;

        try
        {
            #region ArgValidation

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol) )
                throw new ApiParameterException("Unsupported value for 'address' parameter.");

            if ( !string.IsNullOrEmpty(hash) && !ArgValidation.CheckString(hash) )
                throw new ApiParameterException("Unsupported value for 'hash' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if ( !string.IsNullOrEmpty(date_less) && !ArgValidation.CheckNumber(date_less) )
                throw new ApiParameterException("Unsupported value for 'date_less' parameter.");

            if ( !string.IsNullOrEmpty(date_greater) && !ArgValidation.CheckNumber(date_greater) )
                throw new ApiParameterException("Unsupported value for 'date_greater' parameter.");

            #endregion

            var startTime = DateTime.Now;

            using MainDbContext databaseContext = new();
            var query = databaseContext.ContractMethods.AsQueryable().AsNoTracking();

            #region Filtering

            if ( !string.IsNullOrEmpty(symbol) ) query = query.Where(x => x.Contract.SYMBOL == symbol);

            if ( !string.IsNullOrEmpty(hash) ) query = query.Where(x => x.Contract.HASH == hash);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Contract.Chain.NAME == chain);

            if ( !string.IsNullOrEmpty(date_less) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS <= UnixSeconds.FromString(date_less));

            if ( !string.IsNullOrEmpty(date_greater) )
                query = query.Where(x => x.TIMESTAMP_UNIX_SECONDS >= UnixSeconds.FromString(date_greater));

            #endregion

            // Count total number of results before adding order and limit parts of query.
            if ( with_total == 1 )
                totalResults = query.Count();

            //in case we add more to sort
            if ( order_direction == "asc" )
                query = order_by switch
                {
                    "id" => query.OrderBy(x => x.ID),
                    "symbol" => query.OrderBy(x => x.Contract.SYMBOL),
                    "name" => query.OrderBy(x => x.Contract.NAME),
                    _ => query
                };
            else
                query = order_by switch
                {
                    "id" => query.OrderByDescending(x => x.ID),
                    "symbol" => query.OrderByDescending(x => x.Contract.SYMBOL),
                    "name" => query.OrderByDescending(x => x.Contract.NAME),
                    _ => query
                };


            contractMethodHistoryArray = query.Skip(offset).Take(limit).Select(x => new ContractMethodHistory
                {
                    contract = new Contract
                    {
                        name = x.Contract.NAME,
                        hash = x.Contract.HASH,
                        symbol = x.Contract.SYMBOL,
                        methods = x.Contract.ContractMethod != null ? x.Contract.ContractMethod.METHODS : null
                    },
                    date = x.TIMESTAMP_UNIX_SECONDS.ToString()
                }
            ).ToArray();


            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("ContractMethodHistoryResult()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new ContractMethodHistoryResult
        {
            total_results = with_total == 1 ? totalResults : null,
            Contract_method_histories = contractMethodHistoryArray
        };
    }
}
