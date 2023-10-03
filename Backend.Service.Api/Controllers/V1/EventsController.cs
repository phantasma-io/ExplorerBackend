using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class EventsController : BaseControllerV1
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns events available on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.EventsResult'>EventsResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are date, token_id or id</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="contract" example="SOUL">Token contract hash</param>
    /// <param name="token_id">Token ID</param>
    /// <param name="date_day">Date day match (matches whole given day)</param>
    /// <param name="date_less">Date (greater than), UTC unixseconds</param>
    /// <param name="date_greater">Date (greater than), UTC unixseconds</param>
    /// <param name="event_kind" example="TokenMint">Event kind name</param>
    /// <param name="event_kind_partial" example="Token">Event kind (partial match)</param>
    /// <param name="nft_name_partial">Nft name (partial match)</param>
    /// <param name="nft_description_partial">Nft description (partial match))</param>
    /// <param name="address">Address (Hash)</param>
    /// <param name="address_partial">Address (partial match) (Hash)</param>
    /// <param name="block_hash"><a href='#model-Backend.Service.Api.Block'>Block</a> hash</param>
    /// <param name="block_height">height of the <a href='#model-Backend.Service.Api.Block'>Block</a></param>
    /// <param name="transaction_hash"><a href='#model-Backend.Service.Api.Transaction'>Transaction</a> hash</param>
    /// <param name="event_id">Internal ID</param>
    /// <param name="with_event_data" example="0">Return event data with more details, needs with_events to be set</param>
    /// <param name="with_metadata" example="0">Return data with <a href='#model-Backend.Service.Api.NftMetadata'>nft metadata</a></param>
    /// <param name="with_series" example="0">Return NFT <a href='#model-Backend.Service.Api.Series'>Series</a></param>
    /// <param name="with_fiat" example="0">
    ///     Return with <a href='#model-Backend.Service.Api.FiatPrice'>fiat_prices</a> (only
    ///     <a href='#model-Backend.Service.Api.MarketEvent'>market_event</a>)
    /// </param>
    /// <param name="with_nsfw" example="0">Include Data that has been marked NSFW</param>
    /// <param name="with_blacklisted" example="0">Include Data that has been marked Blacklisted</param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("events")]
    [ApiInfo(typeof(EventsResult), "Returns events available on the chain", cacheDuration: 10, cacheTag: "eventKinds")]
    public Task<EventsResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "id",
        [FromQuery] string order_direction = "asc",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string chain = "main",
        [FromQuery] string contract = "",
        [FromQuery] string token_id = "",
        [FromQuery] string date_day = "",
        [FromQuery] string date_less = "",
        [FromQuery] string date_greater = "",
        [FromQuery] string event_kind = "",
        [FromQuery] string event_kind_partial = "",
        [FromQuery] string nft_name_partial = "",
        [FromQuery] string nft_description_partial = "",
        [FromQuery] string address = "",
        [FromQuery] string address_partial = "",
        [FromQuery] string block_hash = "",
        [FromQuery] string block_height = "",
        [FromQuery] string transaction_hash = "",
        [FromQuery] string event_id = "",
        [FromQuery] int with_event_data = 0,
        [FromQuery] int with_metadata = 0,
        [FromQuery] int with_series = 0,
        [FromQuery] int with_fiat = 0,
        [FromQuery] int with_nsfw = 0,
        [FromQuery] int with_blacklisted = 0,
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return GetEvents.Execute(
            order_by,
            order_direction,
            offset,
            limit,
            chain,
            contract,
            token_id,
            date_day,
            date_less,
            date_greater,
            event_kind,
            event_kind_partial,
            nft_name_partial,
            nft_description_partial,
            address,
            address_partial,
            block_hash,
            block_height,
            transaction_hash,
            event_id,
            with_event_data,
            with_metadata,
            with_series,
            with_fiat,
            with_nsfw,
            with_blacklisted,
            with_total);
    }
}
