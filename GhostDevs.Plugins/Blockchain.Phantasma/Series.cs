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
    private void NewSeriesLoad(int chainId)
    {
        var startTime = DateTime.Now;

        var updatedSeriesCount = 0;

        // Initializing supply etc.
        using ( var databaseContext = new MainDbContext() )
        {
            // First we get symbols that has uninitialized series.
            var seriesList = databaseContext.Serieses.Where(x =>
                x.Contract.ChainId == chainId && ( x.SeriesMode == null || x.Nfts.Count > x.CURRENT_SUPPLY ) &&
                x.SERIES_ID != null).ToList();
            Log.Information("[{Name}] Series that needs to be updated: {Series}", Name, seriesList.Count);

            var tokenCache = new Dictionary<string, JsonDocument>();

            foreach ( var series in seriesList )
            {
                // Then we get series id's of series that are uninitialized for given symbol.
                var seriesListForSymbol = seriesList.Where(x => x.Contract.SYMBOL == series.Contract.SYMBOL).ToList();

                if ( !tokenCache.TryGetValue(series.Contract.SYMBOL, out var token) )
                {
                    token = Client.ApiRequest<JsonDocument>(
                        $"{Settings.Default.GetRest()}/api/getToken?symbol=" + series.Contract.SYMBOL +
                        "&extended=true", out var stringResponse, null, 120);
                    if ( token != null ) tokenCache.Add(series.Contract.SYMBOL, token);
                }

                if ( token == null )
                {
                    Log.Error("[{Name}] Series update failed: token is null", Name);
                    continue;
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
                        var seriesToUpdate = seriesListForSymbol.FirstOrDefault(x => x.SERIES_ID == seriesId);
                        if ( seriesToUpdate == null ) continue;

                        seriesToUpdate.CURRENT_SUPPLY =
                            int.Parse(entry.GetProperty("currentSupply").GetString() ?? string.Empty);
                        seriesToUpdate.MAX_SUPPLY =
                            int.Parse(entry.GetProperty("maxSupply").GetString() ?? string.Empty);
                        seriesToUpdate.SeriesMode = SeriesModeMethods.Upsert(databaseContext,
                            entry.GetProperty("mode").GetString(), false);
                    }

                updatedSeriesCount++;
            }

            if ( updatedSeriesCount > 0 ) databaseContext.SaveChanges();
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information("[{Name}] Series update took {UpdateTime} sec, {UpdatedSeriesCount} series updated", Name,
            Math.Round(updateTime.TotalSeconds, 3), updatedSeriesCount);
    }
}
