using System;
using System.Linq;
using System.Text.Json;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Serilog;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private void ContractDataSync(int chainId)
    {
        var startTime = DateTime.Now;
        var unixSecondsNow = UnixSeconds.Now();

        var contractUpdatedCount = 0;

        using MainDbContext databaseContext = new();

        //statement here
        var contracts = databaseContext.Contracts.Where(x =>
                x.ChainId == chainId && ( x.LAST_UPDATED_UNIX_SECONDS == 0 ||
                                          x.LAST_UPDATED_UNIX_SECONDS < UnixSeconds.AddMinutes(unixSecondsNow, -30) ))
            .ToList();

        DateTime transactionStart;
        TimeSpan transactionEnd;

        var chainEntry = ChainMethods.Get(databaseContext, chainId);

        foreach ( var contract in contracts )
        {
            var url =
                $"{Settings.Default.GetRest()}/api/v1/getContract?chainAddressOrName={chainEntry.NAME}&contractName={contract.NAME}";
            var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 10);
            if ( response == null )
            {
                Log.Error("[{Name}] Contract sync: null result", Name);
                continue;
            }

            transactionStart = DateTime.Now;
            if ( response.RootElement.TryGetProperty("address", out var addressProperty) )
            {
                var address = addressProperty.GetString();
                var addressEntry = AddressMethods.Get(databaseContext, chainEntry, address);
                if ( addressEntry != null )
                    if ( contract.Address != addressEntry )
                        contract.Address = addressEntry;
            }

            if ( response.RootElement.TryGetProperty("script", out var scriptProperty) )
            {
                var script = scriptProperty.GetString();
                if ( !string.IsNullOrEmpty(script) && contract.SCRIPT_RAW != script )
                    contract.SCRIPT_RAW = script;
            }

            //we have never filled the data, at least pull the current info we have
            if ( contract.ContractMethod == null )
                if ( response.RootElement.TryGetProperty("methods", out var methodsProperty) )
                {
                    //0 just that we have a value here
                    var method = ContractMethodMethods.Insert(databaseContext, contract, methodsProperty, 0);
                    contract.ContractMethod = method;
                }

            contract.LAST_UPDATED_UNIX_SECONDS = UnixSeconds.Now();

            transactionEnd = DateTime.Now - transactionStart;
            Log.Verbose("[{Name}] Contract {Contract} processed in {Time} sec", Name, contract.NAME,
                Math.Round(transactionEnd.TotalSeconds, 3));

            contractUpdatedCount++;
        }

        transactionStart = DateTime.Now;
        databaseContext.SaveChanges();
        transactionEnd = DateTime.Now - transactionStart;
        Log.Verbose("[{Name}] Processed Commit in {Time} sec", Name, Math.Round(transactionEnd.TotalSeconds, 3));

        var updateTime = DateTime.Now - startTime;
        if(updateTime.TotalSeconds > 1 || contractUpdatedCount > 0)
        {
            Log.Information("[{Name}] Contract sync took {Time} sec, {Updated} names updated", Name,
                Math.Round(updateTime.TotalSeconds, 3), contractUpdatedCount);
        }
    }
}
