using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Castle.Core.Internal;
using Database.Main;
using Phantasma.Core;
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
                var organizationAddress = OrganizationAddressMethods.GetOrganizationsByAddress(databaseContext, address.ADDRESS);
                if ( organizationAddress != null ) address.Organizations = organizationAddress.ToList();
                databaseContext.Update(address);

                processed++;
                if ( processed % saveAfterCount != 0 ) continue;
                try
                {
                    transactionStart = DateTime.Now;
                    databaseContext.SaveChanges();
                    transactionEnd = DateTime.Now - transactionStart;
                    Log.Verbose("[{Name}] Processed Commit in {Time} sec", Name,
                        Math.Round(transactionEnd.TotalSeconds, 3));
                }
                catch ( Exception e )
                {
                    Log.Verbose("Error: {e}" ,e.Message);
                }
               
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
    
    private void AddressDataSyncList(int chainId)
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
            var total = addressesToUpdate.Count();
            var times = total / 100;
            if ( times == 0 ) times = 1;
            var splitAddresses = addressesToUpdate.Chunk(100);
            for ( int i = 0; i < splitAddresses.Count(); i++ )
            {
                var splited = splitAddresses.ElementAt(i).Select(x => x.ADDRESS).ToList();
                var addresses = string.Join(",", splited);
                var url = $"{Settings.Default.GetRest()}/api/v1/getAccounts?accountText={addresses}";
                var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 1000);
                if ( response == null )
                {
                    Log.Error("[{Name}] Names sync: null result", Name);
                    continue;
                }

                var accounts = response.RootElement.EnumerateArray().ToList();
                foreach ( var account in accounts )
                {
                    var address =
                        addressesToUpdate.FirstOrDefault(x => x.ADDRESS == account.GetProperty("address").GetString());
                    if ( address == null ) continue;

                    var name = account.GetProperty("name").GetString();
                    if ( name == "anonymous" ) name = null;

                    if ( address.ADDRESS_NAME != name )
                    {
                        address.ADDRESS_NAME = name;
                        namesUpdatedCount++;
                    }

                    address.NAME_LAST_UPDATED_UNIX_SECONDS = UnixSeconds.Now();

                    if ( account.TryGetProperty("txs", out var transactionProperty) )
                    {
                        transactionStart = DateTime.Now;
                        var transactions = transactionProperty.EnumerateArray()
                            .Select(transaction => transaction.ToString()).ToList();
                        AddressTransactionMethods.InsertIfNotExists(databaseContext, address, transactions, false);

                        transactionEnd = DateTime.Now - transactionStart;
                        Log.Verbose("[{Name}] Processed {Count} TransactionAddresses in {Time} sec", Name,
                            transactions.Count, Math.Round(transactionEnd.TotalSeconds, 3));
                    }

                    if ( account.TryGetProperty("stakes", out var stakesProperty) )
                    {
                        var amount = stakesProperty.GetProperty("amount").GetString();
                        var unclaimed = stakesProperty.GetProperty("unclaimed").GetString();

                        AddressStakeMethods.Upsert(databaseContext, address,
                            Commons.Utils.ToDecimal(amount, soulDecimals), amount,
                            stakesProperty.GetProperty("time").GetInt32(),
                            Commons.Utils.ToDecimal(unclaimed, kcalDecimals), unclaimed, false);
                    }

                    if ( account.TryGetProperty("balances", out var balancesProperty) )
                    {
                        transactionStart = DateTime.Now;
                        var balancesList = balancesProperty.EnumerateArray().Select(balance =>
                                new Tuple<string, string, string>(balance.GetProperty("chain").GetString(),
                                    balance.GetProperty("symbol").GetString(),
                                    balance.GetProperty("amount").GetString()))
                            .ToList();

                        AddressBalanceMethods.InsertOrUpdateList(databaseContext, address, balancesList, false);

                        transactionEnd = DateTime.Now - transactionStart;
                        Log.Verbose("[{Name}] Processed {Count} Balances in {Time} sec", Name,
                            balancesList.Count, Math.Round(transactionEnd.TotalSeconds, 3));
                    }

                    if ( account.TryGetProperty("storage", out var storageProperty) )
                        AddressStorageMethods.Upsert(databaseContext, address,
                            storageProperty.GetProperty("available").GetUInt32(),
                            storageProperty.GetProperty("used").GetUInt32(),
                            storageProperty.GetProperty("avatar").GetString(), false);
                            
                    
                    var stake = account.GetProperty("stake").GetString();
                    address.STAKE = Commons.Utils.ToDecimal(stake, soulDecimals);
                    address.STAKE_RAW = stake;
                    var unclaimedStorage = account.GetProperty("unclaimed").GetString();
                    address.UNCLAIMED = Commons.Utils.ToDecimal(unclaimedStorage, kcalDecimals);
                    address.UNCLAIMED_RAW = unclaimedStorage;

                    var validatorKind = AddressValidatorKindMethods.Upsert(databaseContext,
                        account.GetProperty("validator").GetString());
                    address.AddressValidatorKind = validatorKind;

                    //just to keep things up2date
                    address.Organization = null;
                    //var organization = OrganizationMethods.Get(databaseContext, address.ADDRESS_NAME);
                    //if ( organization != null ) address.Organization = organization;
                    //var organizations = OrganizationAddressMethods.GetOrganizationsByAddress(databaseContext, address.ADDRESS);
                    //if ( organizations.Any() ) address.Organizations = organizations.ToList();

                    processed++;
                    if ( processed % saveAfterCount != 0 ) continue;
                    transactionStart = DateTime.Now;
                    databaseContext.SaveChanges();
                    transactionEnd = DateTime.Now - transactionStart;
                    Log.Verbose("[{Name}] Processed Commit in {Time} sec", Name,
                        Math.Round(transactionEnd.TotalSeconds, 3));
                }
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

    private async Task<Address> SyncAddressByNameAsync(MainDbContext databaseContext, Chain chain, string addressName, Organization organization)
    {
        var startTime = DateTime.Now;

        if ( string.IsNullOrEmpty(addressName) ) return null;

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

            if ( string.IsNullOrEmpty(address) ) return null;

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
                addressEntry = await AddressMethods.UpsertAsync(databaseContext, chain, address);
                if ( name == "anonymous" ) name = null;

                addressEntry.ADDRESS_NAME = name;
            }
        }

        if ( organization != null )
        {
            Log.Verbose("[{Name}] setting Organization {Organization} for Address {Address}", Name,
                organization.ORGANIZATION_ID, addressEntry.ADDRESS);
            addressEntry.Organization = organization;
            if ( addressEntry.Organizations == null ) addressEntry.Organizations = new List<Organization>();
            addressEntry.Organizations.AddDistinct(organization);
            organization.ADDRESS_NAME = addressName;
            organization.ADDRESS = addressEntry.ADDRESS;
        }

        var lookUpTime = DateTime.Now - startTime;
        Log.Information("[{Name}] Address lookup for {AddressName} took {Time} sec", Name,
            addressName, Math.Round(lookUpTime.TotalSeconds, 3));

        return addressEntry;
    }

    // TODO: Finish this feature 
    private void FetchAllAddressesBySymbol(MainDbContext databaseContext, string chain, string symbol, bool extended = false)
    {
         var startTime = DateTime.Now;

        if ( string.IsNullOrEmpty(symbol) ) return;

        var chainEntity = ChainMethods.Get(databaseContext, chain);
        
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
        //do not process everything here, let the sync to that later, we just call it to make sure
        foreach ( var addressElement in addressesArray )
        {
            var addressAddress = addressElement.GetProperty("address").GetString();
            var addressEntry = AddressMethods.Get(databaseContext, chainEntity, addressAddress);

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
                }
                catch ( Exception e )
                {
                    Log.Error(e, "Error while inserting address {Address}", addressAddress);
                }

                continue;
            }
        }

        //addresses.Add(addressAddress);
        
        AddressMethods.InsertIfNotExists(databaseContext, chainEntity, addresses);
        
        var lookUpTime = DateTime.Now - startTime;
        Log.Information("Get all addresses by symbol took {Time} sec", Math.Round(lookUpTime.TotalSeconds, 3));
    }
    
    private void FetchAllAddresses(string chain)
    {
        MainDbContext databaseContext = new();
        var tokens = TokenMethods.GetSupportedTokens(databaseContext);
        foreach ( var token in tokens)
        {
            Log.Verbose("[{Symbol}] Fetching all the users.", token.NativeSymbol);

            MainDbContext databaseContext2 = new();
            FetchAllAddressesBySymbol(databaseContext2, chain, token.NativeSymbol, false);
            databaseContext2.SaveChanges();
        }
    }
}
