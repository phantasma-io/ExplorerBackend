using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class EventKindsController : BaseControllerV1
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Event Kinds on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.EventKindResult'>EventKindResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or name</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="event_kind" example="TokenMint">eventKind name</param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("eventKinds")]
    [ApiInfo(typeof(EventKindResult), "Returns event kinds available on the chain", cacheDuration: 600, cacheTag: "eventKinds")]
    public Task<EventKindResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "id",
        [FromQuery] string order_direction = "asc",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string event_kind = "",
        [FromQuery] string chain = "main",
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return Task.FromResult(Endpoints.EventKinds(
            order_by,
            order_direction,
            offset,
            limit,
            event_kind,
            chain,
            with_total));
    }
}
