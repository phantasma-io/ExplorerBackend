using System;
using System.Linq;
using System.Net;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Chain = GhostDevs.Service.ApiResults.Chain;

namespace GhostDevs.Service;

public partial class Endpoints
{
    /// <summary>
    ///     Returns the Chains on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-ChainResult'>ChainResult</a>
    /// </remarks>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [ProducesResponseType(typeof(ChainResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(ChainResult), "Returns the chains on the backend.", false, 10)]
    public ChainResult Chains(
        // ReSharper disable InconsistentNaming
        int offset = 0,
        int limit = 50,
        string chain = "",
        int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        Chain[] chainArray;

        try
        {
            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new ApiParameterException("Unsupported value for 'limit' parameter.");

            if ( !ArgValidation.CheckOffset(offset) )
                throw new ApiParameterException("Unsupported value for 'offset' parameter.");

            var startTime = DateTime.Now;

            using MainDbContext databaseContext = new();
            var query = databaseContext.Chains.AsQueryable().AsNoTracking();

            if ( !string.IsNullOrEmpty(chain) )
                query = query.Where(x => x.NAME == chain);

            if ( with_total == 1 )
                totalResults = query.Count();

            chainArray = query.Skip(offset).Take(limit).Select(x => new Chain
            {
                chain_name = x.NAME,
                chain_height = x.CURRENT_HEIGHT
            }).ToArray();

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch ( ApiParameterException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Chains()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new ChainResult {total_results = with_total == 1 ? totalResults : null, chains = chainArray};
    }
}
