using System;
using System.Linq;
using System.Text.Json;
using Backend.Api;
using Backend.PluginEngine;
using Database.Main;
using Serilog;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private void ContractMethodSync()
    {
        var startTime = DateTime.Now;
        var contractMethodsUpdated = 0;

        Log.Verbose("[{Name}] checking if there is work in the Queue {Bool}", Name, _methodQueue.Any());

        using MainDbContext databaseContext = new();

        while ( _methodQueue.Any() )
        {
            var (contractString, chainInt, timestampUnixSeconds) = _methodQueue.Peek();
            var chainItem = ChainMethods.Get(databaseContext, chainInt);
            var contractItem = ContractMethods.Get(databaseContext, chainItem, contractString);

            if ( chainItem == null || contractItem == null )
            {
                _methodQueue.Dequeue();
                continue;
            }

            Log.Verbose("[{Name}] got Item from Queue, Contract {Contract} and Chain {Chain}", Name, contractItem.NAME,
                chainItem.NAME);

            var url =
                $"{Settings.Default.GetRest()}/api/v1/getContract?chainAddressOrName={chainItem.NAME}&contractName={contractItem.NAME}";
            var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 10);
            if ( response == null )
            {
                Log.Error("[{Name}] Contract Update sync: null result, dequeue", Name);
                _methodQueue.Dequeue();
                continue;
            }

            if ( response.RootElement.TryGetProperty("methods", out var methodsProperty) )
            {
                var method = ContractMethodMethods.Insert(databaseContext, contractItem, methodsProperty,
                    timestampUnixSeconds, false);
                contractItem.ContractMethod = method;
                contractMethodsUpdated++;
            }

            _methodQueue.Dequeue();
            Log.Verbose("[{Name}] processed and dequeued, go on with next {Bool}", Name, _methodQueue.Any());
        }

        if ( contractMethodsUpdated <= 0 ) return;

        var transactionStart = DateTime.Now;
        databaseContext.SaveChanges();
        var transactionEnd = DateTime.Now - transactionStart;
        Log.Verbose("[{Name}] Processed Commit in {Time} sec", Name, Math.Round(transactionEnd.TotalSeconds, 3));

        var updateTime = DateTime.Now - startTime;
        Log.Information("[{Name}] ContractMethod sync took {Time} sec, {Updated} names updated", Name,
            Math.Round(updateTime.TotalSeconds, 3), contractMethodsUpdated);
    }
}
