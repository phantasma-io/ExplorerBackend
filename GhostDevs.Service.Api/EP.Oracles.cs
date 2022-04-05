using System;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
using Oracle = GhostDevs.Service.ApiResults.Oracle;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(OracleResult), "Returns the addresses on the backend.", false, 10)]
    public OracleResult Oracles(
        [APIParameter("Order by [id, url, content]", "string")]
        string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("Block Hash", "string")] string block_hash = "",
        [APIParameter("Block Height", "string")]
        string block_height = "",
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0)
    {
        long totalResults = 0;
        Oracle[] oracleArray;

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

                if ( !string.IsNullOrEmpty(block_hash) && !ArgValidation.CheckHash(block_hash) )
                    throw new APIException("Unsupported value for 'block_hash' parameter.");

                if ( !string.IsNullOrEmpty(block_height) && !ArgValidation.CheckNumber(block_height) )
                    throw new APIException("Unsupported value for 'block_height' parameter.");

                if ( string.IsNullOrEmpty(block_hash) && string.IsNullOrEmpty(block_height) )
                    throw new APIException("Need either block_hash or block_height != null");

                var startTime = DateTime.Now;

                var query = databaseContext.BlockOracles.AsQueryable();

                if ( !string.IsNullOrEmpty(block_hash) )
                    query = query.Where(x => x.Block.HASH == block_hash);

                if ( !string.IsNullOrEmpty(block_height) )
                    query = query.Where(x => x.Block.HEIGHT == block_height);

                // Count total number of results before adding order and limit parts of query.
                if ( with_total == 1 )
                    totalResults = query.Count();

                //in case we add more to sort
                if ( order_direction == "asc" )
                    query = order_by switch
                    {
                        "id" => query.OrderBy(x => x.Oracle.ID),
                        "url" => query.OrderBy(x => x.Oracle.URL),
                        "content" => query.OrderBy(x => x.Oracle.CONTENT),
                        _ => query
                    };
                else
                    query = order_by switch
                    {
                        "id" => query.OrderByDescending(x => x.Oracle.ID),
                        "url" => query.OrderByDescending(x => x.Oracle.URL),
                        "content" => query.OrderByDescending(x => x.Oracle.CONTENT),
                        _ => query
                    };

                oracleArray = query.Skip(offset).Take(limit).Select(x => new Oracle
                {
                    url = x.Oracle.URL,
                    content = x.Oracle.URL
                }).ToArray();

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

        return new OracleResult {total_results = with_total == 1 ? totalResults : null, oracles = oracleArray};
    }
}
