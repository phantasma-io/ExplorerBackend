using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Backend.Api;
using Backend.Commons;
using Backend.PluginEngine;
using Database.Main;
using Microsoft.EntityFrameworkCore;
using Phantasma.Core;
using Serilog;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private async Task UpdateAddressesBalancesAsync(MainDbContext databaseContext, Chain chain, List<string> addresses, int updateChunkSize)
    {
        var startTime = DateTime.Now;
        
        var processed = 0;

        var addressesToUpdate = await databaseContext.Addresses.Where(x =>
            x.Chain == chain && addresses.Contains(x.ADDRESS)).ToListAsync();
        Log.Verbose("[{Name}] got {Count} Addresses to check", Name, addressesToUpdate.Count);

        var soulDecimals = TokenMethods.GetSoulDecimals(databaseContext, chain);
        var kcalDecimals = TokenMethods.GetKcalDecimals(databaseContext, chain);

        var splitAddresses = addressesToUpdate.Chunk(updateChunkSize).ToList();
        for ( var i = 0; i < splitAddresses.Count; i++ )
        {
            var split = splitAddresses.ElementAt(i).Select(x => x.ADDRESS).ToList();
            var addressesComaSeparated = string.Join(",", split);
            var url = $"{Settings.Default.GetRest()}/api/v1/getAccounts?accountText={addressesComaSeparated}&extended=false";

            try
            {
                var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 1000);

                if ( response == null )
                {
                    Log.Error("[{Name}] Balance sync: null result", Name);
                    if ( updateChunkSize > 1 )
                    {
                        // Temp solution for situation when batch of addresses crash API
                        await UpdateAddressesBalancesAsync(databaseContext, chain, split, 1);
                    }
                    continue;
                }

                var accounts = response.RootElement.EnumerateArray().ToList();
                foreach ( var account in accounts )
                {
                    var address =
                        addressesToUpdate.FirstOrDefault(x => x.ADDRESS == account.GetProperty("address").GetString());
                    if ( address == null ) continue;

                    var name = account.GetProperty("name").GetString();
                    if ( name == "anonymous" )
                    {
                        name = null;
                    }
                    address.ADDRESS_NAME = name;                    
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
                        var balancesList0 = balancesProperty.EnumerateArray();

                        if(balancesList0.Count() > 0)
                        {
                            var balancesList = balancesList0.Select(balance =>
                                    new Tuple<string, string, string>(balance.GetProperty("chain").GetString(),
                                        balance.GetProperty("symbol").GetString(),
                                        balance.GetProperty("amount").GetString()))
                                .ToList();

                            await AddressBalanceMethods.InsertOrUpdateList(databaseContext, address, balancesList);
                        }
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

                    // TODO some bs to fix later
                    //just to keep things up2date
                    address.Organization = null;
                    //var organization = OrganizationMethods.Get(databaseContext, address.ADDRESS_NAME);
                    //if ( organization != null ) address.Organization = organization;
                    //var organizations = OrganizationAddressMethods.GetOrganizationsByAddress(databaseContext, address.ADDRESS);
                    //if ( organizations.Any() ) address.Organizations = organizations.ToList();

                    processed++;
                }
            }
            catch
            {
                Log.Error("[{Name}] Crashed while querying url: {url}", Name, url);
                throw;
            }
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information("[{Name}] Address sync took {Time} sec, Processed {Processed}", Name,
            Math.Round(updateTime.TotalSeconds, 3), processed);
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
                url = $"{Settings.Default.GetRest()}/api/v1/getAccount?account={address}&extended=false";
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
