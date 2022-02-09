using System;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
using Contract = GhostDevs.Service.ApiResults.Contract;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(ContractResult), "Returns the contracts on the backend.", false, 10)]
    public ContractResult Contracts(
        [APIParameter("Order by [id, name, symbol]", "string")]
        string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("symbol", "string")] string symbol = "",
        [APIParameter("hash", "string")] string hash = "",
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0)
    {
        long totalResults = 0;
        Contract[] contractArray;

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

                if ( !string.IsNullOrEmpty(symbol) && !ArgValidation.CheckSymbol(symbol) )
                    throw new APIException("Unsupported value for 'address' parameter.");

                if ( !string.IsNullOrEmpty(hash) && !ArgValidation.CheckString(hash) )
                    throw new APIException("Unsupported value for 'hash' parameter.");

                var startTime = DateTime.Now;

                var query = databaseContext.Contracts.AsQueryable();

                if ( !string.IsNullOrEmpty(symbol) )
                    query = query.Where(x => string.Equals(x.SYMBOL.ToUpper(), symbol.ToUpper()));

                if ( !string.IsNullOrEmpty(hash) )
                    query = query.Where(x => string.Equals(x.HASH.ToUpper(), hash.ToUpper()));

                // Count total number of results before adding order and limit parts of query.
                if ( with_total == 1 )
                    totalResults = query.Count();

                //in case we add more to sort
                if ( order_direction == "asc" )
                    query = order_by switch
                    {
                        "id" => query.OrderBy(x => x.ID),
                        "symbol" => query.OrderBy(x => x.SYMBOL),
                        "name" => query.OrderBy(x => x.NAME),
                        _ => query
                    };
                else
                    query = order_by switch
                    {
                        "id" => query.OrderByDescending(x => x.ID),
                        "symbol" => query.OrderByDescending(x => x.SYMBOL),
                        "name" => query.OrderByDescending(x => x.NAME),
                        _ => query
                    };

                var queryResults = query.Skip(offset).Take(limit).ToList();

                contractArray = queryResults.Select(x => new Contract
                    {
                        name = x.NAME,
                        hash = ContractMethods.Prepend0x(x.HASH, x.Chain.NAME),
                        symbol = x.SYMBOL
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
                var logMessage = LogEx.Exception("Address()", exception);

                throw new APIException(logMessage, exception);
            }
        }

        return new ContractResult {total_results = with_total == 1 ? totalResults : null, contracts = contractArray};
    }
}
