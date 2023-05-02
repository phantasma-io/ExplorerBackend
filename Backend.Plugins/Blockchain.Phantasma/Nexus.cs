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
    private void InitNexusData(int chainId)
    {
        var startTime = DateTime.Now;

        int updatedTokensCount;
        int updatedPlatformsCount;
        int updatedOrganizationsCount;

        using ( MainDbContext databaseContext = new() )
        {
            updatedTokensCount = 0;
            updatedPlatformsCount = 0;
            updatedOrganizationsCount = 0;
            var url = $"{Settings.Default.GetRest()}/api/v1/getNexus?extended=true";

            //TODO fix
            PlatformMethods.Upsert(databaseContext, "phantasma", null, null, false, true);
            updatedPlatformsCount++;

            DateTime transactionStart;
            TimeSpan transactionEnd;

            var chainEntry = ChainMethods.Get(databaseContext, chainId);

            var response = Client.ApiRequest<JsonDocument>(url, out var stringResponse, null, 10);
            if ( response != null )
            {
                if ( response.RootElement.TryGetProperty("error", out var errorProperty) )
                    Log.Error("[{Name}] Cannot fetch Token info. Error: {Error}",
                        Name, errorProperty.GetString());

                //platforms, first, might need it for tokens
                if ( response.RootElement.TryGetProperty("platforms", out var platformsProperty) )
                {
                    var platforms = platformsProperty.EnumerateArray();
                    foreach ( var platform in platforms )
                    {
                        var platformName = platform.GetProperty("platform").GetString();
                        var chainHash = platform.GetProperty("chain").GetString();
                        var fuel = platform.GetProperty("fuel").GetString();

                        //create platform now
                        var platformItem =
                            PlatformMethods.Upsert(databaseContext, platformName, chainHash, fuel, false);

                        if ( platform.TryGetProperty("tokens", out var platformTokenProperty) )
                        {
                            transactionStart = DateTime.Now;
                            var tokenList = platformTokenProperty.EnumerateArray().Select(token => token.ToString())
                                .ToList();
                            PlatformTokenMethods.InsertIfNotExists(databaseContext, tokenList, platformItem, false);
                            transactionEnd = DateTime.Now - transactionStart;
                            Log.Verbose("[{Name}] Processed {Count} PlatformTokens in {Time} sec", Name,
                                tokenList.Count, Math.Round(transactionEnd.TotalSeconds, 3));
                        }

                        if ( platform.TryGetProperty("interop", out var platformInteropProperty) )
                        {
                            transactionStart = DateTime.Now;
                            var interopList = platformInteropProperty.EnumerateArray().Select(interop =>
                                new Tuple<string, string>(interop.GetProperty("local").GetString(),
                                    interop.GetProperty("external").GetString())).ToList();

                            PlatformInteropMethods.InsertIfNotExists(databaseContext, interopList, chainEntry,
                                platformItem, false);

                            transactionEnd = DateTime.Now - transactionStart;
                            Log.Verbose("[{Name}] Processed {Count} InteropItems in {Time} sec", Name,
                                interopList.Count, Math.Round(transactionEnd.TotalSeconds, 3));
                        }

                        Log.Verbose(
                            "[{Name}] got Platform {Platform}, Hash {Hash}, fuel {Fuel}",
                            Name, platformName ?? string.Empty, chainHash ?? string.Empty, fuel ?? string.Empty);

                        updatedPlatformsCount++;
                    }
                }

                //tokens
                if ( response.RootElement.TryGetProperty("tokens", out var tokensProperty) )
                {
                    var tokens = tokensProperty.EnumerateArray();

                    foreach ( var token in tokens )
                    {
                        var tokenSymbol = token.GetProperty("symbol").GetString();
                        var tokenName = token.GetProperty("name").GetString();
                        var tokenDecimal = token.GetProperty("decimals").GetInt32();
                        var currentSupply = token.GetProperty("currentSupply").GetString();
                        var maxSupply = token.GetProperty("maxSupply").GetString();
                        var burnedSupply = token.GetProperty("burnedSupply").GetString();
                        var address = token.GetProperty("address").GetString();
                        var owner = token.GetProperty("owner").GetString();
                        var scriptRaw = token.GetProperty("script").GetString();

                        var fungible = false;
                        var transferable = false;
                        var finite = false;
                        var divisible = false;
                        var fuel = false;
                        var stakable = false;
                        var fiat = false;
                        var swappable = false;
                        var burnable = false;
                        var mintable = false;

                        if ( token.TryGetProperty("flags", out var flags) )
                            if ( flags.ToString().Contains("Fungible") )
                                fungible = true;
                            else if ( flags.ToString().Contains("Transferable") )
                                transferable = true;
                            else if ( flags.ToString().Contains("Finite") )
                                finite = true;
                            else if ( flags.ToString().Contains("Divisible") )
                                divisible = true;
                            else if ( flags.ToString().Contains("Fuel") )
                                fuel = true;
                            else if ( flags.ToString().Contains("Stakable") )
                                stakable = true;
                            else if ( flags.ToString().Contains("Fiat") )
                                fiat = true;
                            else if ( flags.ToString().Contains("Swappable") )
                                swappable = true;
                            else if ( flags.ToString().Contains("Burnable") )
                                burnable = true;
                            else if ( flags.ToString().Contains("Mintable") )
                                mintable = true;


                        var tokenEntry = TokenMethods.Upsert(databaseContext, chainEntry, tokenSymbol, tokenSymbol,
                            tokenDecimal, fungible, transferable, finite, divisible, fuel, stakable, fiat, swappable,
                            burnable, mintable, address, owner, currentSupply, maxSupply, burnedSupply, scriptRaw,
                            false);

                        if ( token.TryGetProperty("external", out var externalsProperty) )
                        {
                            transactionStart = DateTime.Now;
                            var externalList = externalsProperty.EnumerateArray().Select(external =>
                                new Tuple<string, string>(external.GetProperty("platform").GetString(),
                                    external.GetProperty("hash").GetString())).ToList();

                            ExternalMethods.InsertIfNotExists(databaseContext, externalList, tokenEntry, false);

                            transactionEnd = DateTime.Now - transactionStart;
                            Log.Verbose("[{Name}] Processed {Count} Externals in {Time} sec", Name,
                                externalList.Count, Math.Round(transactionEnd.TotalSeconds, 3));
                        }
                        
                        // TODO: Add the fetch for address / Tokens
                        //FetchAllAddressesBySymbol(databaseContext, chainEntry, tokenSymbol, false, false );

                        Log.Verbose(
                            "[{Name}] got Token Symbol {Symbol}, Name {TokenName}, Fungible {Fungible}, Decimal {Decimal}, Database Id {Id}",
                            Name, tokenSymbol, tokenName, fungible, tokenDecimal, tokenEntry.ID);

                        updatedTokensCount++;
                    }
                }

                //technically not needed for tokens, but we still got the data here
                if ( response.RootElement.TryGetProperty("organizations", out var organizationsProperty) )
                {
                    var organizations = organizationsProperty.EnumerateArray();

                    foreach ( var organization in organizations )
                    {
                        Log.Verbose(
                            "[{Name}] got Platform {Organization}",
                            Name, organization.ToString());

                        var urlOrg =
                            $"{Settings.Default.GetRest()}/api/v1/getOrganization?ID={organization.ToString()}";
                        var responseOrg = Client.ApiRequest<JsonDocument>(urlOrg, out var stringResponseOrg, null, 10);
                        if ( responseOrg != null )
                        {
                            if ( responseOrg.RootElement.TryGetProperty("error", out var errorPropertyOrg) )
                                Log.Error("[{Name}] Cannot fetch Organization info. Error: {Error}",
                                    Name, errorPropertyOrg.GetString());

                            var organizationId = responseOrg.RootElement.GetProperty("id").ToString();
                            var organizationName = responseOrg.RootElement.GetProperty("name").ToString();
                            //try to find address, if not found we need to trigger a lookUpName and then getAddress and insert it

                            var orgItem = OrganizationMethods.Upsert(databaseContext, organizationId, organizationName,
                                false);
                            var addressEntry = SyncAddressByName(chainEntry, organizationId, orgItem, false);
                            orgItem.Address = addressEntry;

                            Log.Verbose(
                                "[{Name}] Organization {OrganizationName}, Address {Address}, AddressName {AddressName}",
                                Name, orgItem.ORGANIZATION_ID, addressEntry.ADDRESS, addressEntry.ADDRESS_NAME);

                            if ( responseOrg.RootElement.TryGetProperty("members", out var membersProperty) )
                            {
                                transactionStart = DateTime.Now;
                                var memberList = membersProperty.EnumerateArray().Select(member => member.ToString())
                                    .ToList();
                                Log.Verbose("[{Name}] got {Count} Addresses to process", Name, memberList.Count);

                                OrganizationAddressMethods.RemoveFromOrganizationAddressesIfNeeded(databaseContext, orgItem, memberList, false);

                                OrganizationAddressMethods.InsertIfNotExists(databaseContext, orgItem, memberList,
                                    chainEntry, false);
                                

                                transactionEnd = DateTime.Now - transactionStart;
                                Log.Verbose("[{Name}] Processed {Count} OrganizationAddresses in {Time} sec", Name,
                                    memberList.Count, Math.Round(transactionEnd.TotalSeconds, 3));
                            }
                        }

                        updatedOrganizationsCount++;
                    }
                }
            }

            if ( updatedTokensCount > 0 || updatedPlatformsCount > 0 || updatedOrganizationsCount > 0 )
            {
                transactionStart = DateTime.Now;
                databaseContext.SaveChanges();
                transactionEnd = DateTime.Now - transactionStart;
                Log.Verbose("[{Name}] Processed Commit in {Time} sec", Name,
                    Math.Round(transactionEnd.TotalSeconds, 3));
            }
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information(
            "[{Name}] Token update took {UpdateTime} sec, {UpdatedTokensCount} tokens updated, {PlatformsUpdated} platforms updated, {OrganizationUpdated} Organizations updated",
            Name, Math.Round(updateTime.TotalSeconds, 3), updatedTokensCount, updatedPlatformsCount,
            updatedOrganizationsCount);
    }
}
