using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class OrganizationsController : BaseControllerV1
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Organizations on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.OrganizationResult'>OrganizationResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id, name or organization_id</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="organization_id" example="validators">Organization id</param>
    /// <param name="organization_id_partial" example="valid">Organization id (partial)</param>
    /// <param name="organization_name" example="Block Producers">Organization Name</param>
    /// <param name="organization_name_partial" example="Block Pro">Organization Name (partial)</param>
    /// <param name="with_creation_event" example="0">Return data with <a href='#model-Backend.Service.Api.Event'>Event</a> of the creation</param>
    /// <param name="with_address" example="0">Return data with <a href='#model-Backend.Service.Api.Address'>Event</a> of the creation</param>
    /// <param name="with_total" example="0">Returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("organizations")]
    [ApiInfo(typeof(OrganizationResult), "Returns organizations available on the chain", cacheDuration: 60, cacheTag: "organizations")]
    public Task<OrganizationResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "name",
        [FromQuery] string order_direction = "asc",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string organization_id = "",
        [FromQuery] string organization_id_partial = "",
        [FromQuery] string organization_name = "",
        [FromQuery] string organization_name_partial = "",
        [FromQuery] int with_creation_event = 0,
        [FromQuery] int with_address = 0,
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return Task.FromResult(Endpoints.Organizations(
            order_by,
            order_direction,
            offset,
            limit,
            organization_id,
            organization_id_partial,
            organization_name,
            organization_name_partial,
            with_creation_event,
            with_address,
            with_total));
    }
}
