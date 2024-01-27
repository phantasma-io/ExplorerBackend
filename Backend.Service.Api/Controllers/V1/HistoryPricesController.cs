using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class HistoryPricesController : BaseControllerV1
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Token Price History on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.HistoryPriceResult'>HistoryPriceResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or name</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="symbol" example="SOUL"></param>
    /// <param name="date_less">Date (greater than), UTC unixseconds</param>
    /// <param name="date_greater">Date (greater than), UTC unixseconds</param>
    /// <param name="with_token" example="0">Return Data with <a href='#model-Backend.Service.Api.Token'>Token</a></param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("historyPrices")]
    [ApiInfo(typeof(HistoryPriceResult), "Returns the token price history", cacheDuration: 60, cacheTag: "historyPrices")]
    public Task<HistoryPriceResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "date",
        [FromQuery] string order_direction = "asc",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string symbol = "SOUL",
        [FromQuery] string date_less = "",
        [FromQuery] string date_greater = "",
        [FromQuery] int with_token = 0,
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return GetHistoryPrices.Execute(
            order_by,
            order_direction,
            offset,
            limit,
            symbol,
            date_less,
            date_greater,
            with_token,
            with_total);
    }
}
