using System;
using System.Linq;
using System.Text.Json;
using Backend.Api;
using Backend.PluginEngine;
using Database.ApiCache;
using Database.Main;
using Serilog;
using ChainMethods = Database.Main.ChainMethods;
using ContractMethods = Database.ApiCache.ContractMethods;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private void InitChains()
    {
        var startTime = DateTime.Now;

        using MainDbContext databaseContext = new();
        using ApiCacheDbContext apiCacheDbContext = new();

        var updatedChainsCount = 0;

        var url = $"{Settings.Default.GetRest()}/api/getChains";

        var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 10);
        if ( response != null )
        {
            var chains = response.RootElement.EnumerateArray();
            Log.Information("[{Name}] got {ChainCount} Chains", Name, chains.Count());

            for ( var idx = 0; idx < chains.Count(); idx++ )
            {
                var element = chains.ElementAt(idx);

                var chainName = element.GetProperty("name").GetString();
                //var currentChainHeight = element.GetProperty("height").GetInt64();
                Log.Information("[{Name}] Chain name is {NameChain}", Name, chainName);

                var chain = ChainMethods.Upsert(databaseContext, chainName, false);

                var apiChain = Database.ApiCache.ChainMethods.Upsert(apiCacheDbContext, chainName);

                Log.Verbose("[{Name}] chain {ChainName} with Database Id {Id} processed, go on with Contracts",
                    Name, chainName, chain.ID);

                if ( element.TryGetProperty("contracts", out var contractsProperty) )
                {
                    var transactionStart = DateTime.Now;

                    var contractList = contractsProperty.EnumerateArray().Select(contract =>
                        new Tuple<string, string>(contract.ToString(), contract.ToString())).ToList();
                    var hashList = contractList.Select(tuple => tuple.Item1).ToList();

                    ContractMethods.InsertIfNotExists(apiCacheDbContext, hashList, apiChain, false);
                    Database.Main.ContractMethods.InsertIfNotExistList(databaseContext, contractList, chain, null,
                        false);

                    var transactionEnd = DateTime.Now - transactionStart;
                    Log.Verbose("[{Name}] Processed {Count} Contracts in {Time} sec", Name,
                        contractList.Count, Math.Round(transactionEnd.TotalSeconds, 3));
                }

                updatedChainsCount++;
            }
        }

        if ( updatedChainsCount > 0 )
        {
            apiCacheDbContext.SaveChanges();
            databaseContext.SaveChanges();
        }


        var updateTime = DateTime.Now - startTime;
        Log.Information(
            "[{Name}] Chains update took {UpdateTime} sec, {UpdatedChainCount} chains updated", Name,
            Math.Round(updateTime.TotalSeconds, 3), updatedChainsCount);
    }
}
