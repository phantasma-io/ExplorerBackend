using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class TokensController : BaseControllerV1
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the token on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.TokenResult'>TokenResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id or symbol</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="symbol" example="SOUL"></param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="with_price" example="0">Return data with <a href='#model-Backend.Service.Api.Price'>Prices</a> </param>
    /// <param name="with_creation_event" example="0">Return data with <a href='#model-Backend.Service.Api.Event'>Event</a> of the creation</param>
    /// <param name="with_logo" example="0">Return data with <a href='#model-Backend.Service.Api.TokenLogo'>Logo</a> Information</param>
    /// <param name="with_total" example="0">Returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("tokens")]
    [ApiInfo(typeof(TokenResult), "Returns tokens available on the chain", cacheDuration: 60, cacheTag: "tokens")]
    public Task<TokenResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "id",
        [FromQuery] string order_direction = "asc",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string symbol = "",
        [FromQuery] string chain = "main",
        [FromQuery] int with_price = 0,
        [FromQuery] int with_creation_event = 0,
        [FromQuery] int with_logo = 0,
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return Task.FromResult(Endpoints.Tokens(
            order_by,
            order_direction,
            offset,
            limit,
            symbol,
            chain,
            with_price,
            with_creation_event,
            with_logo,
            with_total));
    }
}
