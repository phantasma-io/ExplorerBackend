using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class ContractsController : BaseControllerV1
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Contracts on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.ContractResult'>ContractResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id, name or symbol</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="symbol" example="SOUL"></param>
    /// <param name="hash" example="SOUL"></param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="with_methods" example="0">Return Data with methods</param>
    /// <param name="with_script" example="0">Return Data with raw script, use instructions to disassemble</param>
    /// <param name="with_token" example="0">Return Data with <a href='#model-Backend.Service.Api.Token'>Token</a></param>
    /// <param name="with_creation_event" example="0">Return data with <a href='#model-Backend.Service.Api.Event'>Event</a> of the creation</param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("contracts")]
    [ApiInfo(typeof(ContractResult), "Returns contracts available on the chain", cacheDuration: 600, cacheTag: "chains")]
    public Task<ContractResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "id",
        [FromQuery] string order_direction = "asc",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string symbol = "",
        [FromQuery] string hash = "",
        [FromQuery] string chain = "main",
        [FromQuery] int with_methods = 0,
        [FromQuery] int with_script = 0,
        [FromQuery] int with_token = 0,
        [FromQuery] int with_creation_event = 0,
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return GetContracts.Execute(
            order_by,
            order_direction,
            offset,
            limit,
            symbol,
            hash,
            chain,
            with_methods,
            with_script,
            with_token,
            with_creation_event,
            with_total);
    }
}
