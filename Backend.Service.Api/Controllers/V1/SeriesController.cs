using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class SeriesController : BaseControllerV1
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns series of NFTs available on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.SeriesResult'>SeriesResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id, series_id or name</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="id">Internal ID</param>
    /// <param name="series_id">Series ID</param>
    /// <param name="creator">Creator of series (Address)</param>
    /// <param name="name">Series name/description filter (partial match)</param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="contract" example="SOUL">Token contract hash</param>
    /// <param name="symbol" example="SOUL"></param>
    /// <param name="token_id">Token ID</param>
    /// <param name="with_total" example="0">Returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("series")]
    [ApiInfo(typeof(SeriesResult), "Returns series of NFTs available on the chain", cacheDuration: 10, cacheTag: "series")]
    public Task<SeriesResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "id",
        [FromQuery] string order_direction = "asc",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string id = "",
        [FromQuery] string series_id = "",
        [FromQuery] string creator = "",
        [FromQuery] string name = "",
        [FromQuery] string chain = "main",
        [FromQuery] string contract = "",
        [FromQuery] string symbol = "",
        [FromQuery] string token_id = "",
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return GetSeries.Execute(
            order_by,
            order_direction,
            offset,
            limit,
            id,
            series_id,
            creator,
            name,
            chain,
            contract,
            symbol,
            token_id,
            with_total);
    }
}
