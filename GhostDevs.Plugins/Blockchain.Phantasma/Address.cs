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
    private void AddressDataSync(int chainId)
    {
        var startTime = DateTime.Now;
        var unixSecondsNow = UnixSeconds.Now();

        const int saveAfterCount = 100;

        var namesUpdatedCount = 0;
        var processed = 0;

        using ( MainDbContext databaseContext = new() )
        {
            var addressesToUpdate = databaseContext.Addresses.Where(x =>
                x.ChainId == chainId && ( x.NAME_LAST_UPDATED_UNIX_SECONDS == 0 ||
                                          x.NAME_LAST_UPDATED_UNIX_SECONDS <
                                          UnixSeconds.AddMinutes(unixSecondsNow, -30) )).ToList();
            Log.Verbose("[{Name}] got {Count} Addresses to check", Name, addressesToUpdate.Count);

            DateTime transactionStart;
            TimeSpan transactionEnd;

            var tokenSoul = TokenMethods.Get(databaseContext, chainId, "SOUL");
            var tokenKcal = TokenMethods.Get(databaseContext, chainId, "KCAL");
            foreach ( var address in addressesToUpdate )
            {
                var url = $"{Settings.Default.GetRest()}/api/getAccount?account={address.ADDRESS}";
                var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 10);
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
                    transactionStart = DateTime.Now;
                    var transactions = transactionProperty.EnumerateArray()
                        .Select(transaction => transaction.ToString()).ToList();
                    AddressTransactionMethods.InsertIfNotExists(databaseContext, address, transactions, false);

                    transactionEnd = DateTime.Now - transactionStart;
                    Log.Verbose("[{Name}] Processed {Count} TransactionAddresses in {Time} sec", Name,
                        transactions.Count, Math.Round(transactionEnd.TotalSeconds, 3));
                }


                if ( response.RootElement.TryGetProperty("stakes", out var stakesProperty) )
                {
                    var amount = Utils.ToDecimal(stakesProperty.GetProperty("amount").GetString(), tokenSoul);
                    var unclaimed = Utils.ToDecimal(stakesProperty.GetProperty("unclaimed").GetString(), tokenKcal);

                    AddressStakeMethods.Upsert(databaseContext, address, amount,
                        stakesProperty.GetProperty("time").GetInt32(), unclaimed, false);
                }

                if ( response.RootElement.TryGetProperty("balances", out var balancesProperty) )
                {
                    transactionStart = DateTime.Now;
                    var balancesList = balancesProperty.EnumerateArray().Select(balance =>
                            new Tuple<string, string, string>(balance.GetProperty("chain").GetString(),
                                balance.GetProperty("symbol").GetString(), balance.GetProperty("amount").GetString()))
                        .ToList();

                    AddressBalanceMethods.InsertOrUpdateList(databaseContext, address, balancesList, false);

                    transactionEnd = DateTime.Now - transactionStart;
                    Log.Verbose("[{Name}] Processed {Count} Balances in {Time} sec", Name,
                        balancesList.Count, Math.Round(transactionEnd.TotalSeconds, 3));
                }

                if ( response.RootElement.TryGetProperty("storage", out var storageProperty) )
                    AddressStorageMethods.Upsert(databaseContext, address,
                        storageProperty.GetProperty("available").GetUInt32(),
                        storageProperty.GetProperty("used").GetUInt32(),
                        storageProperty.GetProperty("avatar").GetString(), false);


                address.STAKE = Utils.ToDecimal(response.RootElement.GetProperty("stake").GetString(), tokenSoul);
                address.UNCLAIMED =
                    Utils.ToDecimal(response.RootElement.GetProperty("unclaimed").GetString(), tokenKcal);
                address.RELAY = response.RootElement.GetProperty("relay").GetString();

                var validatorKind = AddressValidatorKindMethods.Upsert(databaseContext,
                    response.RootElement.GetProperty("validator").GetString());
                address.AddressValidatorKind = validatorKind;

                processed++;
                if ( processed % saveAfterCount != 0 ) continue;
                transactionStart = DateTime.Now;
                databaseContext.SaveChanges();
                transactionEnd = DateTime.Now - transactionStart;
                Log.Verbose("[{Name}] Processed Commit in {Time} sec", Name,
                    Math.Round(transactionEnd.TotalSeconds, 3));
            }

            transactionStart = DateTime.Now;
            databaseContext.SaveChanges();
            transactionEnd = DateTime.Now - transactionStart;
            Log.Verbose("[{Name}] Processed Commit in {Time} sec", Name, Math.Round(transactionEnd.TotalSeconds, 3));
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information("[{Name}] Address sync took {Time} sec, {Updated} names updated, Processed {Processed}", Name,
            Math.Round(updateTime.TotalSeconds, 3), namesUpdatedCount, processed);
    }
}
