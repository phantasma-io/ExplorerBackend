using System;
using System.Linq;
using System.Text.Json;
using Database.Main;
using GhostDevs.Api;
using GhostDevs.Commons;
using GhostDevs.PluginEngine;
using Serilog;

namespace GhostDevs.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    public void NameSync()
    {
        var startTime = DateTime.Now;
        var unixSecondsNow = UnixSeconds.Now();

        var namesUpdatedCount = 0;

        using ( MainDbContext databaseContext = new() )
        {
            var addressesToUpdate = databaseContext.Addresses.Where(x =>
                x.ChainId == ChainId && ( x.NAME_LAST_UPDATED_UNIX_SECONDS == 0 ||
                                          x.NAME_LAST_UPDATED_UNIX_SECONDS <
                                          UnixSeconds.AddMinutes(unixSecondsNow, -30) )).ToList();

            foreach ( var address in addressesToUpdate )
            {
                var url = $"{Settings.Default.GetRest()}/api/getAccount?account={address.ADDRESS}";
                var response = Client.APIRequest<JsonDocument>(url, out var stringResponse, null, 10);
                if ( response == null )
                {
                    Log.Error("[{Name}] Names sync: null result", Name);
                    continue;
                }

                var name = response.RootElement.GetProperty("name").GetString();
                if ( name == "anonymous" ) name = null;

                if ( address.ADDRESS_NAME != name )
                {
                    address.ADDRESS_NAME = name;
                    namesUpdatedCount++;
                }

                address.NAME_LAST_UPDATED_UNIX_SECONDS = UnixSeconds.Now();
            }

            databaseContext.SaveChanges();
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information("[{Name}] Names sync took {Time} sec, {Updated} names updated", Name,
            Math.Round(updateTime.TotalSeconds, 3), namesUpdatedCount);
    }
}
