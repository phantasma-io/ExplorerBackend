using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class PlatformsController : BaseControllerV1
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Platform on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.PlatformResult'>PlatformResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or name</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="name" example="neo">Platform name</param>
    /// <param name="with_external" example="0">Return Data with <a href='#model-Backend.Service.Api.External'>External</a></param>
    /// <param name="with_interops" example="0">Return Data with <a href='#model-Backend.Service.Api.PlatformInterop'>Interops</a></param>
    /// <param name="with_token" example="0">Return Data with <a href='#model-Backend.Service.Api.Token'>Token</a></param>
    /// <param name="with_creation_event" example="0">Return data with <a href='#model-Backend.Service.Api.Event'>Event</a> of the creation</param>
    /// <param name="with_total" example="0">Returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("platforms")]
    [ApiInfo(typeof(PlatformResult), "Returns platforms available on the chain", cacheDuration: 60, cacheTag: "platforms")]
    public Task<PlatformResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "id",
        [FromQuery] string order_direction = "asc",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string name = "",
        [FromQuery] int with_external = 0,
        [FromQuery] int with_interops = 0,
        [FromQuery] int with_token = 0,
        [FromQuery] int with_creation_event = 0,
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return Task.FromResult(Endpoints.Platforms(
            order_by,
            order_direction,
            offset,
            limit,
            name,
            with_external,
            with_interops,
            with_token,
            with_creation_event,
            with_total));
    }
}
