using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetOracles
{
    [ProducesResponseType(typeof(OracleResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(OracleResult), "Returns the Oracles on the backend.", false, 10)]
    public static async Task<OracleResult> Execute(
        // ReSharper disable InconsistentNaming
        string order_by = "id",
        string order_direction = "asc",
        int offset = 0,
        int limit = 50,
        string block_hash = "",
        string block_height = "",
        string chain = "main",
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
                throw new ApiParameterException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new ApiParameterException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimit(limit, filter) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            if ( !string.IsNullOrEmpty(block_hash) && !ArgValidation.CheckHash(block_hash) )
                throw new ApiParameterException("Unsupported value for 'block_hash' parameter.");

            if ( !string.IsNullOrEmpty(block_height) && !ArgValidation.CheckNumber(block_height) )
                throw new ApiParameterException("Unsupported value for 'block_height' parameter.");

            if ( string.IsNullOrEmpty(block_hash) && string.IsNullOrEmpty(block_height) )
                throw new ApiParameterException("Need either block_hash or block_height != null");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();
            var query = databaseContext.BlockOracles.AsQueryable().AsNoTracking();

            if ( !string.IsNullOrEmpty(block_hash) )
                query = query.Where(x => x.Block.HASH == block_hash);

            if ( !string.IsNullOrEmpty(block_height) )
                query = query.Where(x => x.Block.HEIGHT == block_height);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Block.Chain.NAME == chain);

            // Count total number of results before adding order and limit parts of query.
            if ( with_total == 1 )
                totalResults = await query.CountAsync();

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

            oracleArray = await query.Select(x => new Oracle
            {
                url = x.Oracle.URL,
                content = x.Oracle.CONTENT
            }).ToArrayAsync();

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Oracles()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new OracleResult {total_results = with_total == 1 ? totalResults : null, oracles = oracleArray};
    }
}
