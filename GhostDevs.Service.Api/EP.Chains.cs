using System;
using System.Linq;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(ChainResult), "Returns the chains on the backend.", false, 10)]
    public ChainResult Chains([APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("Chain name (ex. 'main')", "string")]
        string chain = "",
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0)
    {
        long totalResults = 0;
        Chain[] chainArray;

        try
        {
            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new APIException("Unsupported value for 'chain' parameter.");

            if ( !ArgValidation.CheckLimit(limit, false) )
                throw new APIException("Unsupported value for 'limit' parameter.");

            var startTime = DateTime.Now;

            var query = _context.Chains.AsQueryable();

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
        catch ( APIException )
        {
            throw;
        }
        catch ( Exception exception )
        {
            var logMessage = LogEx.Exception("Chains()", exception);

            throw new APIException(logMessage, exception);
        }

        return new ChainResult {total_results = with_total == 1 ? totalResults : null, chains = chainArray};
    }
}
