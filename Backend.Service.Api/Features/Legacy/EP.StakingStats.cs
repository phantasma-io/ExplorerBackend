using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetStakingStats
{
    [ProducesResponseType(typeof(StakingStatsResult), (int)HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(StakingStatsResult),
        "Returns staking and soul masters historical statistics for dashboard charts.",
        false, 30, cacheTag: "stakingStats")]
    public static async Task<StakingStatsResult> Execute(
        // ReSharper disable InconsistentNaming
        string chain = "main",
        int daily_limit = 0,
        int monthly_limit = 0
        // ReSharper enable InconsistentNaming
    )
    {
        try
        {
            if (!string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain))
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if (daily_limit < 0 || daily_limit > 20000)
                throw new ApiParameterException("Unsupported value for 'daily_limit' parameter.");

            if (monthly_limit < 0 || monthly_limit > 20000)
                throw new ApiParameterException("Unsupported value for 'monthly_limit' parameter.");

            var startTime = DateTime.Now;
            await using MainDbContext databaseContext = new();

            int? chainId = null;
            if (!string.IsNullOrEmpty(chain))
            {
                chainId = await databaseContext.Chains.AsNoTracking()
                    .Where(x => x.NAME == chain)
                    .Select(x => (int?)x.ID)
                    .FirstOrDefaultAsync();
            }

            if (!chainId.HasValue)
            {
                return new StakingStatsResult
                {
                    chain = chain,
                    daily_limit = daily_limit,
                    monthly_limit = monthly_limit,
                    daily_points_total = 0,
                    monthly_points_total = 0,
                    daily = Array.Empty<StakingDailyStat>(),
                    monthly = Array.Empty<SoulMastersMonthlyStat>()
                };
            }

            var dailyQuery = databaseContext.StakingProgressDailies.AsNoTracking()
                .Where(x => x.ChainId == chainId.Value)
                .Select(x => new StakingDailyStat
                {
                    date_unix_seconds = x.DATE_UNIX_SECONDS,
                    staked_soul_raw = x.STAKED_SOUL_RAW,
                    soul_supply_raw = x.SOUL_SUPPLY_RAW,
                    stakers_count = x.STAKERS_COUNT,
                    masters_count = x.MASTERS_COUNT,
                    staking_ratio = x.STAKING_RATIO,
                    staking_percent = 0m,
                    captured_at_unix_seconds = x.CAPTURED_AT_UNIX_SECONDS,
                    source = x.SOURCE
                });

            var monthlyQuery = databaseContext.SoulMastersMonthlies.AsNoTracking()
                .Where(x => x.ChainId == chainId.Value)
                .Select(x => new SoulMastersMonthlyStat
                {
                    month_unix_seconds = x.MONTH_UNIX_SECONDS,
                    masters_count = x.MASTERS_COUNT,
                    captured_at_unix_seconds = x.CAPTURED_AT_UNIX_SECONDS,
                    source = x.SOURCE
                });

            StakingDailyStat[] dailyData;
            if (daily_limit > 0)
            {
                var limited = await dailyQuery
                    .OrderByDescending(x => x.date_unix_seconds)
                    .Take(daily_limit)
                    .ToArrayAsync();

                dailyData = limited
                    .OrderBy(x => x.date_unix_seconds)
                    .ToArray();
            }
            else
            {
                dailyData = await dailyQuery
                    .OrderBy(x => x.date_unix_seconds)
                    .ToArrayAsync();
            }

            foreach (var item in dailyData)
            {
                item.staking_percent = item.staking_ratio * 100m;
            }

            SoulMastersMonthlyStat[] monthlyData;
            if (monthly_limit > 0)
            {
                var limited = await monthlyQuery
                    .OrderByDescending(x => x.month_unix_seconds)
                    .Take(monthly_limit)
                    .ToArrayAsync();

                monthlyData = limited
                    .OrderBy(x => x.month_unix_seconds)
                    .ToArray();
            }
            else
            {
                monthlyData = await monthlyQuery
                    .OrderBy(x => x.month_unix_seconds)
                    .ToArrayAsync();
            }

            var latestDaily = dailyData.LastOrDefault();

            var responseTime = DateTime.Now - startTime;
            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));

            return new StakingStatsResult
            {
                chain = chain,
                daily_limit = daily_limit,
                monthly_limit = monthly_limit,
                daily_points_total = dailyData.LongLength,
                monthly_points_total = monthlyData.LongLength,
                first_daily_date_unix_seconds = dailyData.FirstOrDefault()?.date_unix_seconds,
                latest_daily_date_unix_seconds = latestDaily?.date_unix_seconds,
                first_month_unix_seconds = monthlyData.FirstOrDefault()?.month_unix_seconds,
                latest_month_unix_seconds = monthlyData.LastOrDefault()?.month_unix_seconds,
                latest_staking_ratio = latestDaily?.staking_ratio,
                latest_staking_percent = latestDaily?.staking_percent,
                latest_staked_soul_raw = latestDaily?.staked_soul_raw,
                latest_soul_supply_raw = latestDaily?.soul_supply_raw,
                latest_stakers_count = latestDaily?.stakers_count,
                latest_masters_count = latestDaily?.masters_count,
                daily = dailyData,
                monthly = monthlyData
            };
        }
        catch (ApiParameterException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logMessage = LogEx.Exception("StakingStats()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }
    }
}
