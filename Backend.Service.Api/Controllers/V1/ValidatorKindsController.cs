using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class ValidatorKindsController : BaseControllerV1
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the ValidatorKinds on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.ValidatorKindResult'>ValidatorKindResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or name</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="validator_kind" example="Invalid">validatorKind name</param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("validatorKinds")]
    [ApiInfo(typeof(ValidatorKindResult), "Returns validator kinds available on the chain", cacheDuration: 60, cacheTag: "validatorKinds")]
    public Task<ValidatorKindResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "id",
        [FromQuery] string order_direction = "asc",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string validator_kind = "",
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return Task.FromResult(Endpoints.ValidatorKinds(
            order_by,
            order_direction,
            offset,
            limit,
            validator_kind,
            with_total));
    }
}
