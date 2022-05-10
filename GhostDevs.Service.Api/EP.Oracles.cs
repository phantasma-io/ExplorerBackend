using System;
using System.Linq;
using System.Net;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Oracle = GhostDevs.Service.ApiResults.Oracle;

namespace GhostDevs.Service;

public partial class Endpoints
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Oracles on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-OracleResult'>OracleResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id, url or content]</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="block_hash"><a href='#model-Block'>Block</a> hash</param>
    /// <param name="block_height">height of the <a href='#model-Block'>Block</a></param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="with_total" example="0">Returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Ok</response>
    [ProducesResponseType(typeof(OracleResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [APIInfo(typeof(OracleResult), "Returns the Oracles on the backend.", false, 10)]
    public OracleResult Oracles(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string block_hash = "",
        string block_height = "",
        string chain = "",
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        Oracle[] oracleArray;

        var filter = !string.IsNullOrEmpty(block_hash);

        try
        {
            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new APIException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new APIException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, filter) )
                throw new APIException("Unsupported value for 'limit' parameter.");

            if ( !string.IsNullOrEmpty(block_hash) && !ArgValidation.CheckHash(block_hash) )
                throw new APIException("Unsupported value for 'block_hash' parameter.");

            if ( !string.IsNullOrEmpty(block_height) && !ArgValidation.CheckNumber(block_height) )
                throw new APIException("Unsupported value for 'block_height' parameter.");

            if ( string.IsNullOrEmpty(block_hash) && string.IsNullOrEmpty(block_height) )
                throw new APIException("Need either block_hash or block_height != null");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new APIException("Unsupported value for 'chain' parameter.");

            var startTime = DateTime.Now;
            using MainDbContext databaseContext = new();
            var query = databaseContext.BlockOracles.AsQueryable().AsNoTracking();

            if ( !string.IsNullOrEmpty(block_hash) )
                query = query.Where(x => x.Block.HASH == block_hash);

            if ( !string.IsNullOrEmpty(block_height) )
                query = query.Where(x => x.Block.HEIGHT == block_height);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Block.Chain.NAME == chain);

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

            if ( limit > 0 ) query = query.Skip(offset).Take(limit);

            oracleArray = query.Select(x => new Oracle
            {
                url = x.Oracle.URL,
                content = x.Oracle.CONTENT
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
            var logMessage = LogEx.Exception("Oracles()", exception);

            throw new APIException(logMessage, exception);
        }

        return new OracleResult {total_results = with_total == 1 ? totalResults : null, oracles = oracleArray};
    }
}
