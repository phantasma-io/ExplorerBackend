using System;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
using Chain = GhostDevs.Service.ApiResults.Chain;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(ChainResult), "Returns the chains on the backend.", false, 10)]
    public ChainResult Chains([APIParameter("Chain name (ex. 'main')", "string")] string chain = "")
    {
        long totalResults;
        Chain[] chainArray;

        using ( var databaseContext = new MainDbContext() )
        {
            try
            {
                if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                    throw new APIException("Unsupported value for 'chain' parameter.");

                var startTime = DateTime.Now;

                var query = databaseContext.Chains.AsQueryable();

                if ( !string.IsNullOrEmpty(chain) )
                    query = query.Where(x => string.Equals(x.NAME.ToUpper(), chain.ToUpper()));

                totalResults = query.Count();

                var queryResults = query.ToList();

                chainArray = queryResults.Select(x => new Chain
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
        }

        return new ChainResult {total_results = totalResults, chains = chainArray};
    }
}
