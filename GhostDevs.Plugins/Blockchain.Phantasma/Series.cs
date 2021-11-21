using GhostDevs.Api;
using Database.Main;
using GhostDevs.PluginEngine;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GhostDevs.Blockchain
{
    public partial class PhantasmaPlugin: Plugin, IBlockchainPlugin
    {
        public void NewSeriesLoad()
        {
            DateTime startTime = DateTime.Now;

            int updatedSeriesCount = 0;

            // Initializing supply etc.
            using (var databaseContext = new MainDatabaseContext())
            {
                // First we get symbols that has uninitialized series.
                var serieses = databaseContext.Serieses.Where(x => x.Contract.ChainId == ChainId && (x.SeriesMode == null || x.Nfts.Count() > x.CURRENT_SUPPLY) && x.SERIES_ID != null).ToList();
                Log.Information($"[{Name}] Series that needs to be updated: {serieses.Count}");

                var tokenCache = new Dictionary<string, JsonDocument>();

                foreach (var series in serieses)
                {
                    // Then we get series id's of series that are uninitialized for given symbol.
                    var seriesesForSymbol = serieses.Where(x => x.Contract.SYMBOL == series.Contract.SYMBOL).ToList();

                    JsonDocument token = null;
                    if (!tokenCache.TryGetValue(series.Contract.SYMBOL, out token))
                    {
                        token = Client.APIRequest<JsonDocument>($"{Settings.Default.GetRest()}/api/getToken?symbol=" + series.Contract.SYMBOL + "&extended=true", out var stringResponse, null, 10);
                        if (token != null)
                            tokenCache.Add(series.Contract.SYMBOL, token);
                    }
                    if (token == null)
                    {
                        Log.Error($"[{Name}] Series update failed: token is null.");
                        return;
                    }

                    TimeSpan downloadTime = DateTime.Now - startTime;

                    startTime = DateTime.Now;

                    // Reading properties
                    if (token.RootElement.TryGetProperty("series", out var seriesNode))
                    {
                        foreach (var entry in seriesNode.EnumerateArray())
                        {
                            var seriesID = entry.GetProperty("seriesID").GetInt64().ToString();
                            var seriesToUpdate = seriesesForSymbol.Where(x => x.SERIES_ID == seriesID).FirstOrDefault();
                            if (seriesToUpdate != null)
                            {
                                seriesToUpdate.CURRENT_SUPPLY = Int32.Parse(entry.GetProperty("currentSupply").GetString());
                                seriesToUpdate.MAX_SUPPLY = Int32.Parse(entry.GetProperty("maxSupply").GetString());
                                seriesToUpdate.SeriesModeId = SeriesMethods.SeriesModesGetId(databaseContext, entry.GetProperty("mode").GetString());
                            }
                        }
                    }

                    updatedSeriesCount++;
                }

                if(updatedSeriesCount > 0)
                    databaseContext.SaveChanges();
            }

            TimeSpan updateTime = DateTime.Now - startTime;
            Log.Information($"[{Name}] Series update took {Math.Round(updateTime.TotalSeconds, 3)} sec, {updatedSeriesCount} series updated");
        }
    }
}
