using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class AssetController : BaseControllerV1
{
    //TODO change order_by and order_direction maybe to enum
    /// <summary>
    ///     Returns the Addresses on the backend.
    /// </summary>
    /// <remarks>
    ///     <a href='#model-Backend.Service.Api.AddressResult'>AddressResult</a>
    /// </remarks>
    /// <param name="order_by" example="id">accepted values are id, address or address_name</param>
    /// <param name="order_direction" example="asc">accepted values are asc or desc</param>
    /// <param name="offset" example="0">positive numeric value, represents the value how many values should be skipped</param>
    /// <param name="limit" example="50">how many values will max be pulled</param>
    /// <param name="chain" example="main">Chain name</param>
    /// <param name="address">hash of an address</param>
    /// <param name="address_name">Name of an Address, if is has one</param>
    /// <param name="address_partial">partial hash of an address</param>
    /// <param name="organization_name">Filter for an Organization Name"</param>
    /// <param name="validator_kind" example="Primary">Filter for a Validator Kind</param>
    /// <param name="with_storage" example="0">returns data with <a href='#model-Backend.Service.Api.AddressStorage'>AddressStorage</a></param>
    /// <param name="with_stakes" example="0">returns data with <a href='#model-Backend.Service.Api.AddressStake'>AddressStake</a></param>
    /// <param name="with_balance" example="0">returns data with <a href='#model-Backend.Service.Api.AddressBalances'>AddressBalances</a></param>
    /// <param name="with_total" example="0">returns data with total_count (slower) or not (faster)</param>
    /// <response code="200">Success</response>
    /// <response code="400">Bad Request</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet("addresses")]
    [ApiInfo(typeof(AddressResult), "Returns addresses available on the chain", cacheDuration: 60, cacheTag: "addresses")]
    public Task<AddressResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string order_by = "id",
        [FromQuery] string order_direction = "asc",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string chain = "main",
        [FromQuery] string address = "",
        [FromQuery] string address_name = "",
        [FromQuery] string address_partial = "",
        [FromQuery] string organization_name = "",
        [FromQuery] string validator_kind = "",
        [FromQuery] int with_storage = 0,
        [FromQuery] int with_stakes = 0,
        [FromQuery] int with_balance = 0,
        [FromQuery] int with_total = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return Task.FromResult(Endpoints.Addresses(
            order_by,
            order_direction,
            offset,
            limit,
            chain,
            address,
            address_name,
            address_partial,
            organization_name,
            validator_kind,
            with_storage,
            with_stakes,
            with_balance,
            with_total));
    }
}
