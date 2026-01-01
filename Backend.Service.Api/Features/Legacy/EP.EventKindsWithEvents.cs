using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Backend.Service.Api;

public static class GetEventKindsWithEvents
{
    [ProducesResponseType(typeof(EventKindResult), (int)HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(EventKindResult), "Returns event kinds that have events on the backend.", false, 10)]
    public static async Task<EventKindResult> Execute(
        // ReSharper disable InconsistentNaming
        string chain = "main",
        int with_total = 0
    // ReSharper enable InconsistentNaming
    )
    {
        long totalResults = 0;
        EventKind[] eventKindArray;

        try
        {
            if (!string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain))
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            var startTime = DateTime.Now;

            await using MainDbContext databaseContext = new();
            var eventKindNames =
                await EventKindMethods.GetAvailableEventKindNamesAsync(databaseContext, chain, true);

            eventKindArray = eventKindNames.Select(x => new EventKind
            {
                name = x
            }).ToArray();

            if (with_total == 1) totalResults = eventKindArray.LongLength;

            var responseTime = DateTime.Now - startTime;

            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));
        }
        catch (ApiParameterException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logMessage = LogEx.Exception("EventKindsWithEvents()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }

        return new EventKindResult
        { total_results = with_total == 1 ? totalResults : null, event_kinds = eventKindArray };
    }
}
