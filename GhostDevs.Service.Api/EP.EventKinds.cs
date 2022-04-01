using System;
using System.Linq;
using Database.Main;
using GhostDevs.Commons;
using GhostDevs.Service.ApiResults;
using Serilog;
using EventKind = GhostDevs.Service.ApiResults.EventKind;

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
        [APIParameter("Return total (slower) or not (faster)", "integer")]
        int with_total = 0)
    {
        long totalResults = 0;
        EventKind[] eventKindArray;

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

                if ( !string.IsNullOrEmpty(event_kind) && !ArgValidation.CheckEventKind(event_kind) )
                    throw new APIException("Unsupported value for 'event_kind' parameter.");

                var startTime = DateTime.Now;

                var query = databaseContext.EventKinds.AsQueryable();

                if ( !string.IsNullOrEmpty(event_kind) )
                    query = query.Where(x => string.Equals(x.NAME.ToUpper(), event_kind.ToUpper()));

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

                eventKindArray = query.Skip(offset).Take(limit).Select(x => new EventKind
                {
                    kind = x.NAME
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
                var logMessage = LogEx.Exception("EventKind()", exception);

                throw new APIException(logMessage, exception);
            }
        }

        return new EventKindResult {total_results = with_total == 1 ? totalResults : null, eventKinds = eventKindArray};
    }
}
