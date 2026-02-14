using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Service.Api.Controllers.V1;

public class StakingStatsController : BaseControllerV1
{
    /// <summary>
    ///     Returns staking and Soul Masters historical statistics for chart visualizations.
    /// </summary>
    /// <param name="chain" example="main">Chain scope for chart data</param>
    /// <param name="daily_limit" example="0">Daily points limit, 0 = all</param>
    /// <param name="monthly_limit" example="0">Monthly points limit, 0 = all</param>
    [HttpGet("stakingStats")]
    [ApiInfo(typeof(StakingStatsResult),
        "Returns staking and soul masters historical statistics for dashboard charts",
        cacheDuration: 30,
        cacheTag: "stakingStats")]
    public Task<StakingStatsResult> GetResults(
        // ReSharper disable InconsistentNaming
        [FromQuery] string chain = "main",
        [FromQuery] int daily_limit = 0,
        [FromQuery] int monthly_limit = 0
        // ReSharper enable InconsistentNaming
    )
    {
        return GetStakingStats.Execute(chain, daily_limit, monthly_limit);
    }
}
