using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Castle.Core.Internal;
using Database.Main;
using Serilog;

namespace Backend.Blockchain;

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

            var chain = ChainMethods.Get(databaseContext, chainId);
            var soulDecimals = TokenMethods.GetSoulDecimals(databaseContext, chain);
            var kcalDecimals = TokenMethods.GetKcalDecimals(databaseContext, chain);
            foreach ( var address in addressesToUpdate )
            {
                var url = $"{Settings.Default.GetRest()}/api/v1/getAccount?account={address.ADDRESS}";
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
                    var amount = stakesProperty.GetProperty("amount").GetString();
                    var unclaimed = stakesProperty.GetProperty("unclaimed").GetString();

                    AddressStakeMethods.Upsert(databaseContext, address,
                        Commons.Utils.ToDecimal(amount, soulDecimals), amount,
                        stakesProperty.GetProperty("time").GetInt32(),
                        Commons.Utils.ToDecimal(unclaimed, kcalDecimals), unclaimed, false);
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

                var stake = response.RootElement.GetProperty("stake").GetString();
                address.STAKE = Commons.Utils.ToDecimal(stake, soulDecimals);
                address.STAKE_RAW = stake;
                var unclaimedStorage = response.RootElement.GetProperty("unclaimed").GetString();
                address.UNCLAIMED = Commons.Utils.ToDecimal(unclaimedStorage, kcalDecimals);
                address.UNCLAIMED_RAW = unclaimedStorage;

                var validatorKind = AddressValidatorKindMethods.Upsert(databaseContext,
                    response.RootElement.GetProperty("validator").GetString());
                address.AddressValidatorKind = validatorKind;

                //just to keep things up2date
                address.Organization = null;
                var organization = OrganizationMethods.Get(databaseContext, address.ADDRESS_NAME);
                if ( organization != null ) address.Organization = organization;

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


    private Address SyncAddressByName(Chain chain, string addressName, Organization organization, bool saveChanges)
    {
        var startTime = DateTime.Now;

        if ( addressName.IsNullOrEmpty() ) return null;

        using MainDbContext databaseContext = new();
        var addressEntry = AddressMethods.GetByName(databaseContext, chain, addressName);

        if ( addressEntry == null )
        {
            var url = $"{Settings.Default.GetRest()}/api/v1/lookUpName?name={addressName}";
            var response = Client.ApiRequest<JsonDocument>(url, out _, null, 10);
            if ( response == null )
            {
                Log.Error("[{Name}] lookUpName: null result", Name);
                return null;
            }

            var address = response.RootElement.GetString();
            Log.Verbose("[{Name}] Found Address {Address} for name {AddressName}", Name, address, addressName);

            if ( address.IsNullOrEmpty() ) return null;

            addressEntry = AddressMethods.Get(databaseContext, chain, address);

            if ( addressEntry != null )
                addressEntry.ADDRESS_NAME = addressName;
            else
            {
                url = $"{Settings.Default.GetRest()}/api/v1/getAccount?account={address}";
                response = Client.ApiRequest<JsonDocument>(url, out _, null, 10);
                if ( response == null )
                {
                    Log.Error("[{Name}] getAccount: null result", Name);
                    return null;
                }

                //do not process everything here, let the sync to that later, we just call it to make sure
                string? name = "anonymous";
                if ( response.RootElement.TryGetProperty("name", out JsonElement jsonName) )
                {
                    name = jsonName.GetString();
                }
                addressEntry = AddressMethods.Upsert(databaseContext, chain, address, saveChanges);
                if ( name == "anonymous" ) name = null;

                addressEntry.ADDRESS_NAME = name;
            }
        }

        if ( organization != null )
        {
            Log.Verbose("[{Name}] setting Organization {Organization} for Address {Address}", Name,
                organization.ORGANIZATION_ID, addressEntry.ADDRESS);
            addressEntry.Organization = organization;
        }

        var lookUpTime = DateTime.Now - startTime;
        Log.Information("[{Name}] Address lookup for {AddressName} took {Time} sec", Name,
            addressName, Math.Round(lookUpTime.TotalSeconds, 3));

        return addressEntry;
    }

    // TODO: Finish this feature 
    private void FetchAllAddressesBySymbol(Chain chain, string symbol, bool extended = false, bool saveChanges = true)
    {
         var startTime = DateTime.Now;

        if ( symbol.IsNullOrEmpty() ) return;
        
        var url = $"{Settings.Default.GetRest()}/api/v1/GetAddressesBySymbol?symbol={symbol}&extended={extended}";
        var response = Client.ApiRequest<JsonDocument>(url, out _, null, 50000);
        if ( response == null )
        {
            Log.Error("[{Name}] GetAddressesBySymbol: null result", Name);
            return;
        }

        var addressesArray = response.RootElement.EnumerateArray();
        Log.Verbose("[{Name}] got {Count} Addresses to check", symbol, addressesArray.Count());

        var addresses = new List<string>();
        using ( MainDbContext databaseContext = new() )
        {
            //do not process everything here, let the sync to that later, we just call it to make sure
            foreach ( var addressElement in addressesArray )
            {
                var addressAddress = addressElement.GetProperty("address").GetString();
                var addressEntry = AddressMethods.Get(databaseContext, chain, addressAddress);

                /*if ( addressEntry == null )
                {
                    addressEntry = AddressMethods.Upsert(databaseContext, chain, addressAddress, saveChanges);
                }*/

                if ( addressEntry == null )
                {
                    try
                    {
                        addresses.Add(addressAddress);

                        //addressEntry = AddressMethods.Upsert(databaseContext, chain, addressAddress, saveChanges);
                    }catch(Exception e)
                    {
                        Log.Error(e, "Error while inserting address {Address}", addressAddress);
                    }
                    continue;
                }

                //addresses.Add(addressAddress);
            }
            AddressMethods.InsertIfNotExists(databaseContext, chain, addresses, !saveChanges);

        }

        var lookUpTime = DateTime.Now - startTime;
        Log.Information("Get all addresses by symbol took {Time} sec", Math.Round(lookUpTime.TotalSeconds, 3));
    }


    private void FetchAllAddresses(Chain chain)
    {
        MainDbContext databaseContext = new();
        var tokens = TokenMethods.GetSupportedTokens(databaseContext);
        foreach ( var token in tokens)
        {
            Log.Verbose("[{Symbol}] Fetching all the users.", token.NativeSymbol);

            FetchAllAddressesBySymbol(chain, token.NativeSymbol, false, true);
        }
    }
}
