using System;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Contract = GhostDevs.Service.ApiResults.Contract;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(ContractMethodHistoryResult), "Returns the contractmethods on the backend.", false, 10)]
    public ContractMethodHistoryResult ContractMethodHistories(
        [APIParameter("Order by [id, name, symbol]", "string")]
        string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("symbol", "string")] string symbol = "",
        [APIParameter("hash", "string")] string hash = "",
        [APIParameter("Chain name (ex. 'main')", "string")]
        string chain = "",
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0)
    {
        long totalResults = 0;
        ContractMethodHistory[] contractMethodHistoryArray;

        try
        {
            #region ArgValidation

            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new APIException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new APIException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new APIException("Unsupported value for 'limit' parameter.");

            if ( !string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol) )
                throw new APIException("Unsupported value for 'address' parameter.");

            if ( !string.IsNullOrEmpty(hash) && !ArgValidation.CheckString(hash) )
                throw new APIException("Unsupported value for 'hash' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new APIException("Unsupported value for 'chain' parameter.");

            #endregion

            var startTime = DateTime.Now;


            var query = _context.ContractMethods.AsQueryable().AsNoTracking();

            #region Filtering

            if ( !string.IsNullOrEmpty(symbol) ) query = query.Where(x => x.Contract.SYMBOL == symbol);

            if ( !string.IsNullOrEmpty(hash) ) query = query.Where(x => x.Contract.HASH == hash);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Contract.Chain.NAME == chain);

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
                        hash = ContractMethods.Prepend0x(x.Contract.HASH, x.Contract.Chain.NAME),
                        symbol = x.Contract.SYMBOL,
                        methods = x.Contract.ContractMethod != null ? x.Contract.ContractMethod.METHODS : null
                    },
                    date = x.TIMESTAMP_UNIX_SECONDS.ToString()
                }
            ).ToArray();


            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( APIException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Contract()", exception);

            throw new APIException(logMessage, exception);
        }

        return new ContractMethodHistoryResult
        {
            total_results = with_total == 1 ? totalResults : null,
            Contract_method_histories = contractMethodHistoryArray
        };
    }
}
