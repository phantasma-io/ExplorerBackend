using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class BlocksController : BaseControllerV1
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Blocks information from backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.BlockResult'>BlockResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or hash</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="hash">block hash</param>
    /// <param name="hash_partial">hash (partial match)</param>
    /// <param name="height">height of the <a href='#model-Backend.Service.Api.Block'>Block</a></param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="date_less">Date (greater than), UTC unixseconds</param>
    /// <param name="date_greater">Date (greater than), UTC unixseconds</param>
    /// <param name="with_transactions" example="0">
    ///     Return data with events of the
    ///     <a href='#model-Backend.Service.Api.TransactionResult'>Transactions</a>
    /// </param>
    /// <param name="with_events" example="0">
    ///     Return event data of <a href='#model-Backend.Service.Api.EventsResult'>events</a>, needs
    ///     with_transactions to be set
    /// </param>
    /// <param name="with_event_data" example="0">Return event data with more details, needs with_event_data to be set</param>
    /// <param name="with_nft" example="0">Return data with <a href='#model-Backend.Service.Api.NftMetadata'>nft metadata</a></param>
    /// <param name="with_fiat" example="0">
    ///     Return with <a href='#model-Backend.Service.Api.FiatPrice'>fiat_prices</a> (only
    ///     <a href='#model-Backend.Service.Api.MarketEvent'>market_event</a>)
    /// </param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("blocks")]
    [ApiInfo(typeof(BlockResult), "Returns blocks available on the chain", cacheDuration: 10, cacheTag: "blocks")]
    public Task<BlockResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "id",
        [FromQuery] string order_direction = "asc",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string hash = "",
        [FromQuery] string hash_partial = "",
        [FromQuery] string height = "",
        [FromQuery] string chain = "main",
        [FromQuery] string date_less = "",
        [FromQuery] string date_greater = "",
        [FromQuery] int with_transactions = 0,
        [FromQuery] int with_events = 0,
        [FromQuery] int with_event_data = 0,
        [FromQuery] int with_nft = 0,
        [FromQuery] int with_fiat = 0,
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return GetBlocks.Execute(
            order_by,
            order_direction,
            offset,
            limit,
            hash,
            hash_partial,
            height,
            chain,
            date_less,
            date_greater,
            with_transactions,
            with_events,
            with_event_data,
            with_nft,
            with_fiat,
            with_total);
    }
}
