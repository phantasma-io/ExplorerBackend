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

                    if ( account.TryGetProperty("stakes", out var stakesProperty) )
                    {
                        var amount = stakesProperty.GetProperty("amount").GetString();
                        var unclaimed = stakesProperty.GetProperty("unclaimed").GetString();

                        address.STAKE_TIMESTAMP = stakesProperty.GetProperty("time").GetInt32();
                        address.STAKED_AMOUNT = Commons.Utils.ToDecimal(amount, soulDecimals);
                        address.STAKED_AMOUNT_RAW = amount;
                        address.UNCLAIMED_AMOUNT = Commons.Utils.ToDecimal(unclaimed, kcalDecimals);
                        address.UNCLAIMED_AMOUNT_RAW = unclaimed;
                    }

                    if ( account.TryGetProperty("balances", out var balancesProperty) )
                    {
                        transactionStart = DateTime.Now;
                        var balancesList = balancesProperty.EnumerateArray().Select(balance =>
                                new Tuple<string, string, string>(balance.GetProperty("chain").GetString(),
                                    balance.GetProperty("symbol").GetString(),
                                    balance.GetProperty("amount").GetString()))
                            .ToList();

                        AddressBalanceMethods.InsertOrUpdateList(databaseContext, address, balancesList);

                        transactionEnd = DateTime.Now - transactionStart;
                        Log.Verbose("[{Name}] Processed {Count} Balances in {Time} sec", Name,
                            balancesList.Count, Math.Round(transactionEnd.TotalSeconds, 3));
                    }

                    if ( account.TryGetProperty("storage", out var storageProperty) )
                    {
                        address.STORAGE_AVAILABLE = storageProperty.GetProperty("available").GetUInt32();
                        address.STORAGE_USED = storageProperty.GetProperty("used").GetUInt32();
                        address.AVATAR = storageProperty.GetProperty("avatar").GetString();
                    }

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
}
