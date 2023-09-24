using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class ChainsController : BaseControllerV1
{
    /// <summary>
    ///     Returns the Chains on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.ChainResult'>ChainResult</a>
    /// </remarks>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("chains")]
    [ApiInfo(typeof(ChainResult), "Returns chains available", cacheDuration: 600, cacheTag: "chains")]
    public Task<ChainResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string chain = "main",
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return Task.FromResult(Endpoints.Chains(
            offset,
            limit,
            chain,
            with_total));
    }
}
