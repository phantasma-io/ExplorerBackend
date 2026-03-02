using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class AddressStatsController : BaseControllerV1
{
    /// <summary>
    ///     Returns historical address growth statistics for chart visualizations.
    /// </summary>
    /// <param name="chain" example="main">Chain scope for chart data</param>
    /// <param name="daily_limit" example="0">Daily points limit, 0 = all</param>
    [HttpGet("addressStats")]
    [ApiInfo(typeof(AddressStatsResult),
        "Returns address growth historical statistics for dashboard charts",
        cacheDuration: 30,
        cacheTag: "addressStats")]
    public Task<AddressStatsResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string chain = "main",
        [FromQuery] int daily_limit = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return GetAddressStats.Execute(chain, daily_limit);
    }
}
