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
    private void NameSync(int chainId)
    {
        var startTime = DateTime.Now;
        var unixSecondsNow = UnixSeconds.Now();

        var namesUpdatedCount = 0;

        using ( MainDbContext databaseContext = new() )
        {
            var addressesToUpdate = databaseContext.Addresses.Where(x =>
                x.ChainId == chainId && ( x.NAME_LAST_UPDATED_UNIX_SECONDS == 0 ||
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

                if ( response.RootElement.TryGetProperty("txs", out var transactionProperty) )
                {
                    var transactions = transactionProperty.EnumerateArray();
                    Log.Verbose("[{Name}] got {Count} Transaction(s) for Address {Address}", Name, transactions.Count(),
                        address.ADDRESS);
                    foreach ( var transaction in transactions )
                        AddressTransactionMethods.Upsert(databaseContext, address, transaction.ToString(), false);
                }

                if ( response.RootElement.TryGetProperty("stakes", out var stakesProperty) )
                    AddressStakeMethods.Upsert(databaseContext, address,
                        stakesProperty.GetProperty("amount").GetString(), stakesProperty.GetProperty("time").GetInt32(),
                        stakesProperty.GetProperty("unclaimed").GetString(), false);

                if ( response.RootElement.TryGetProperty("balances", out var balancesProperty) )
                {
                    var balances = balancesProperty.EnumerateArray();
                    Log.Verbose("[{Name}] got {Count} Balance(s) for Address {Address}", Name, balances.Count(),
                        address.ADDRESS);
                    foreach ( var balance in balances )
                        AddressBalanceMethods.Upsert(databaseContext, address, balance.GetProperty("chain").GetString(),
                            balance.GetProperty("symbol").GetString(), balance.GetProperty("amount").GetString(),
                            false);
                }

                if ( response.RootElement.TryGetProperty("storage", out var storageProperty) )
                    AddressStorageMethods.Upsert(databaseContext, address,
                        storageProperty.GetProperty("available").GetUInt32(),
                        storageProperty.GetProperty("used").GetUInt32(),
                        storageProperty.GetProperty("avatar").GetString(), false);

                address.STAKE = response.RootElement.GetProperty("stake").GetString();
                address.UNCLAIMED = response.RootElement.GetProperty("unclaimed").GetString();
                address.RELAY = response.RootElement.GetProperty("relay").GetString();
            }

            databaseContext.SaveChanges();
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information("[{Name}] Names sync took {Time} sec, {Updated} names updated", Name,
            Math.Round(updateTime.TotalSeconds, 3), namesUpdatedCount);
    }
}
