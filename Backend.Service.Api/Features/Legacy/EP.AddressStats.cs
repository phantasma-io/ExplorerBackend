using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Backend.Commons;
using Database.Main;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Backend.Service.Api;

public static class GetAddressStats
{
    private const long SecondsPerDay = 86400;

    [ProducesResponseType(typeof(AddressStatsResult), (int)HttpStatusCode.OK)]
    [HttpGet]
    [ApiInfo(typeof(AddressStatsResult),
        "Returns address growth historical statistics for dashboard charts.",
        false, 30, cacheTag: "addressStats")]
    public static async Task<AddressStatsResult> Execute(
        // ReSharper disable InconsistentNaming
        string chain = "main",
        int daily_limit = 0
    // ReSharper enable InconsistentNaming
    )
    {
        try
        {
            if (!string.IsNullOrEmpty(chain) && !ArgValidation.CheckChain(chain))
                throw new ApiParameterException("Unsupported value for 'chain' parameter.");

            if (daily_limit < 0 || daily_limit > 20000)
                throw new ApiParameterException("Unsupported value for 'daily_limit' parameter.");

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
                return new AddressStatsResult
                {
                    chain = chain,
                    daily_limit = daily_limit,
                    new_addresses_points_total = 0,
                    new_addresses_daily = Array.Empty<NewAddressesDailyStat>()
                };
            }

            var firstTxUnixSeconds = await databaseContext.Addresses.AsNoTracking()
                .Where(x => x.ChainId == chainId.Value &&
                            x.ADDRESS != "NULL" &&
                            x.FIRST_TX_UNIX_SECONDS.HasValue)
                .Select(x => x.FIRST_TX_UNIX_SECONDS!.Value)
                .ToArrayAsync();

            if (firstTxUnixSeconds.Length == 0)
            {
                return new AddressStatsResult
                {
                    chain = chain,
                    daily_limit = daily_limit,
                    new_addresses_points_total = 0,
                    new_addresses_daily = Array.Empty<NewAddressesDailyStat>()
                };
            }

            var newAddressesByDay = firstTxUnixSeconds
                .Select(UnixSeconds.GetDate)
                .GroupBy(x => x)
                .ToDictionary(x => x.Key, x => x.LongCount());

            var startDayUnixSeconds = newAddressesByDay.Keys.Min();
            var endDayUnixSeconds = Math.Max(newAddressesByDay.Keys.Max(), UnixSeconds.GetDate(UnixSeconds.Now()));

            var rows = new List<NewAddressesDailyStat>();
            var cumulative = 0L;
            for (var dayUnixSeconds = startDayUnixSeconds; dayUnixSeconds <= endDayUnixSeconds;
                 dayUnixSeconds += SecondsPerDay)
            {
                newAddressesByDay.TryGetValue(dayUnixSeconds, out var newAddressesCount);
                cumulative += newAddressesCount;

                rows.Add(new NewAddressesDailyStat
                {
                    date_unix_seconds = dayUnixSeconds,
                    new_addresses_count = newAddressesCount,
                    cumulative_addresses_count = cumulative
                });
            }

            if (daily_limit > 0 && rows.Count > daily_limit)
                rows = rows.Skip(rows.Count - daily_limit).ToList();

            var latestDaily = rows.LastOrDefault();

            var responseTime = DateTime.Now - startTime;
            Log.Information("API result generated in {ResponseTime} sec", Math.Round(responseTime.TotalSeconds, 3));

            return new AddressStatsResult
            {
                chain = chain,
                daily_limit = daily_limit,
                new_addresses_points_total = rows.LongCount(),
                first_new_addresses_date_unix_seconds = rows.FirstOrDefault()?.date_unix_seconds,
                latest_new_addresses_date_unix_seconds = latestDaily?.date_unix_seconds,
                latest_new_addresses_count = latestDaily?.new_addresses_count,
                latest_cumulative_addresses_count = latestDaily?.cumulative_addresses_count,
                new_addresses_daily = rows.ToArray()
            };
        }
        catch (ApiParameterException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var logMessage = LogEx.Exception("AddressStats()", exception);
            throw new ApiUnexpectedException(logMessage, exception);
        }
    }
}
