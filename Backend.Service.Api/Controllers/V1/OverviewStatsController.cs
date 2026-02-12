using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class OverviewStatsController : BaseControllerV1
{
    /// <summary>
    ///     Returns aggregated counters for overview/dashboard screens.
    /// </summary>
    /// <param name="chain" example="main">Chain scope for chain-bound counters</param>
    /// <param name="include_burned" example="0">Include burned NFTs in nft totals</param>
    /// <param name="include_legacy_transactions" example="1">
    ///     Include legacy generation transaction chains for main
    /// </param>
    [HttpGet("overviewStats")]
    [ApiInfo(typeof(OverviewStatsResult), "Returns aggregated overview counters", cacheDuration: 10,
        cacheTag: "overviewStats")]
    public Task<OverviewStatsResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string chain = "main",
        [FromQuery] int include_burned = 0,
        [FromQuery] int include_legacy_transactions = 1
    // ReSharper enable InconsistentNaming
    )
    {
        return GetOverviewStats.Execute(chain, include_burned, include_legacy_transactions);
    }
}
