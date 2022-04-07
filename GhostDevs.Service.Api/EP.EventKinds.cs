using System;
using System.Linq;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;

namespace GhostDevs.Service;

public partial class Endpoints
{
    [APIInfo(typeof(EventKindResult), "Returns the eventKinds on the backend.", false, 10)]
    public EventKindResult EventKinds([APIParameter("Order by [id, name]", "string")] string order_by = "id",
        [APIParameter("Order direction [asc, desc]", "string")]
        string order_direction = "asc",
        [APIParameter("Offset", "integer")] int offset = 0,
        [APIParameter("Limit", "integer")] int limit = 50,
        [APIParameter("eventKind name (ex. 'TokenMint')", "string")]
        string event_kind = "",
        [APIParameter("Chain name (ex. 'main')", "string")]
        string chain = "",
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0)
    {
        long totalResults = 0;
        EventKind[] eventKindArray;

        try
        {
            if ( !string.IsNullOrEmpty(order_by) && !ArgValidation.CheckFieldName(order_by) )
                throw new APIException("Unsupported value for 'order_by' parameter.");

            if ( !ArgValidation.CheckOrderDirection(order_direction) )
                throw new APIException("Unsupported value for 'order_direction' parameter.");

            if ( !ArgValidation.CheckLimitOffset(limit, offset) )
                throw new APIException("Unsupported value for 'limit' and/or 'offset' parameter.");

            if ( !string.IsNullOrEmpty(event_kind) && !ArgValidation.CheckString(event_kind, true) )
                throw new APIException("Unsupported value for 'event_kind' parameter.");

            if ( !string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain) )
                throw new APIException("Unsupported value for 'chain' parameter.");

            var startTime = DateTime.Now;

            var query = _context.EventKinds.AsQueryable();

            if ( !string.IsNullOrEmpty(event_kind) ) query = query.Where(x => x.NAME == event_kind);

            if ( !string.IsNullOrEmpty(chain) ) query = query.Where(x => x.Chain.NAME == chain);

            // Count total number of results before adding order and limit parts of query.
            if ( with_total == 1 )
                totalResults = query.Count();

            //in case we add more to sort
            if ( order_direction == "asc" )
                query = order_by switch
                {
                    "id" => query.OrderBy(x => x.ID),
                    "name" => query.OrderBy(x => x.NAME),
                    _ => query
                };
            else
                query = order_by switch
                {
                    "id" => query.OrderByDescending(x => x.ID),
                    "name" => query.OrderByDescending(x => x.NAME),
                    _ => query
                };

            if ( limit > 0 && offset >= 0 ) query = query.Skip(offset).Take(limit);

            eventKindArray = query.Select(x => new EventKind
            {
                name = x.NAME
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
            var logMessage = LogEx.Exception("EventKinds()", exception);

            throw new APIException(logMessage, exception);
        }

        return new EventKindResult
            {total_results = with_total == 1 ? totalResults : null, event_kinds = eventKindArray};
    }
}
