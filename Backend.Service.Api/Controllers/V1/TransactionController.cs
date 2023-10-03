using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class TransactionController : BaseControllerV1
{
    /// <summary>
    ///     Returns the Transaction Information on the backend
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.TransactionResult'>TransactionResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or hash</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="hash"><a href='#model-Backend.Service.Api.Transaction'>Transaction</a> hash</param>
    /// <param name="hash_partial"><a href='#model-Backend.Service.Api.Transaction'>Transaction</a> hash (partial match)</param>
    /// <param name="address">Address (Hash)</param>
    /// <param name="date_less">Date (greater than), UTC unixseconds</param>
    /// <param name="date_greater">Date (greater than), UTC unixseconds</param>
    /// <param name="block_hash"><a href='#model-Backend.Service.Api.Block'>Block</a> hash</param>
    /// <param name="block_height">height of the <a href='#model-Backend.Service.Api.Block'>Block</a></param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="with_nft" example="0">Return data with <a href='#model-Backend.Service.Api.NftMetadata'>nft metadata</a></param>
    /// <param name="with_events" example="0">Return event data of <a href='#model-Backend.Service.Api.EventsResult'>events</a></param>
    /// <param name="with_event_data" example="0">Return event data with more details, needs with_events to be set</param>
    /// <param name="with_fiat" example="0">
    ///     Return with <a href='#model-Backend.Service.Api.FiatPrice'>fiat_prices</a> (only
    ///     <a href='#model-Backend.Service.Api.MarketEvent'>market_event</a>)
    /// </param>
    /// <param name="with_script" example="0">Return with script data</param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("transaction")]
    [ApiInfo(typeof(TransactionResult), "Returns transaction available on the chain", cacheDuration: 5, cacheTag: "transaction")]
    public Task<TransactionResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "id",
        [FromQuery] string order_direction = "asc",
        [FromQuery] string hash = "",
        [FromQuery] string hash_partial = "",
        [FromQuery] string address = "",
        [FromQuery] string date_less = "",
        [FromQuery] string date_greater = "",
        [FromQuery] string block_hash = "",
        [FromQuery] string block_height = "",
        [FromQuery] string chain = "main",
        [FromQuery] int with_nft = 0,
        [FromQuery] int with_events = 0,
        [FromQuery] int with_event_data = 0,
        [FromQuery] int with_fiat = 0,
        [FromQuery] int with_script = 0,
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return GetTransactions.Execute(
            order_by,
            order_direction,
            0,
            1,
            hash,
            hash_partial,
            address,
            date_less,
            date_greater,
            block_hash,
            block_height,
            chain,
            with_nft,
            with_events,
            with_event_data,
            with_fiat,
            with_script,
            with_total);
    }
}
