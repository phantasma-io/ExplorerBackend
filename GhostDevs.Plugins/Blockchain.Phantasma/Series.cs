using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Database.Main;
using GhostDevs.Api;
using GhostDevs.PluginEngine;
using Serilog;

namespace GhostDevs.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    public void NewSeriesLoad()
    {
        var startTime = DateTime.Now;

        var updatedSeriesCount = 0;

        // Initializing supply etc.
        using ( var databaseContext = new MainDbContext() )
        {
            // First we get symbols that has uninitialized series.
            var serieses = databaseContext.Serieses.Where(x =>
                x.Contract.ChainId == ChainId && ( x.SeriesMode == null || x.Nfts.Count > x.CURRENT_SUPPLY ) &&
                x.SERIES_ID != null).ToList();
            Log.Information("[{Name}] Series that needs to be updated: {Serieses}", Name, serieses.Count);

            var tokenCache = new Dictionary<string, JsonDocument>();

            foreach ( var series in serieses )
            {
                // Then we get series id's of series that are uninitialized for given symbol.
                var seriesesForSymbol =
                    serieses.Where(x => x.Contract.SYMBOL == series.Contract.SYMBOL).ToList();

                JsonDocument token = null;
                if ( !tokenCache.TryGetValue(series.Contract.SYMBOL, out token) )
                {
                    token = Client.APIRequest<JsonDocument>(
                        $"{Settings.Default.GetRest()}/api/getToken?symbol=" + series.Contract.SYMBOL +
                        "&extended=true", out var stringResponse, null, 10);
                    if ( token != null ) tokenCache.Add(series.Contract.SYMBOL, token);
                }

                if ( token == null )
                {
                    Log.Error("[{Name}] Series update failed: token is null", Name);
                    return;
                }

                var downloadTime = DateTime.Now - startTime;
                Log.Verbose("[{Name}] download took {DownloadTime} sec for Symbol {Symbol}", Name, downloadTime,
                    series.Contract.SYMBOL);

                startTime = DateTime.Now;

                // Reading properties
                if ( token.RootElement.TryGetProperty("series", out var seriesNode) )
                    foreach ( var entry in seriesNode.EnumerateArray() )
                    {
                        var seriesId = entry.GetProperty("seriesID").GetInt64().ToString();
                        var seriesToUpdate = seriesesForSymbol.FirstOrDefault(x => x.SERIES_ID == seriesId);
                        if ( seriesToUpdate == null ) continue;

                        seriesToUpdate.CURRENT_SUPPLY =
                            int.Parse(entry.GetProperty("currentSupply").GetString() ?? string.Empty);
                        seriesToUpdate.MAX_SUPPLY =
                            int.Parse(entry.GetProperty("maxSupply").GetString() ?? string.Empty);
                        seriesToUpdate.SeriesModeId = SeriesMethods.SeriesModesGetId(databaseContext,
                            entry.GetProperty("mode").GetString());
                    }

                updatedSeriesCount++;
            }

            if ( updatedSeriesCount > 0 ) databaseContext.SaveChanges();
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information(
            "[{Name}] Series update took {UpdateTime} sec, {UpdatedSeriesCount} series updated", Name,
            Math.Round(updateTime.TotalSeconds, 3), updatedSeriesCount);
    }
}
