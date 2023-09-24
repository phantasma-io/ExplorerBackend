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
    [ProducesResponseType(typeof(ChainResult), ( int ) HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(ChainResult), "Returns the chains on the backend.", false, 10)]
    public static ChainResult Chains(
        // ReSharper disable InconsistentNaming
        int offset = 0,
        int limit = 50,
        string chain = "main",
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
