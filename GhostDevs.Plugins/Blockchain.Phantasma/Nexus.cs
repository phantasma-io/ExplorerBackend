using System;
using System.Linq;
using System.Text.Json;
using Database.Main;
using GhostDevs.Api;
using GhostDevs.PluginEngine;
using Serilog;

namespace GhostDevs.Blockchain;

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
            var url = $"{Settings.Default.GetRest()}/api/getNexus";

            //TODO fix
            PlatformMethods.Upsert(databaseContext, "phantasma", null, null, false);
            updatedPlatformsCount++;

            var response = Client.APIRequest<JsonDocument>(url, out var stringResponse, null, 10);
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
                            var tokens = platformTokenProperty.EnumerateArray();
                            foreach ( var token in tokens )
                                PlatformTokenMethods.Upsert(databaseContext, token.ToString(), platformItem);
                        }

                        if ( platform.TryGetProperty("interop", out var platformInteropProperty) )
                        {
                            var interopList = platformInteropProperty.EnumerateArray();
                            foreach ( var interop in interopList )
                            {
                                var local = interop.GetProperty("local").GetString();
                                var external = interop.GetProperty("external").GetString();
                                PlatformInteropMethods.Upsert(databaseContext, local, external, chainId, platformItem,
                                    false);
                            }
                        }

                        Log.Verbose(
                            "[{Name}] got Platform {Platform}, Hash {Hash}, fuel {Fuel}",
                            Name, platformName, chainHash, fuel);

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


                        var id = TokenMethods.Upsert(databaseContext, chainId, tokenSymbol, tokenSymbol, tokenDecimal,
                            fungible, transferable, finite, divisible, fuel, stakable, fiat, swappable, burnable,
                            address, owner, currentSupply, maxSupply, burnedSupply, scriptRaw, false);

                        if ( token.TryGetProperty("external", out var externalsProperty) )
                        {
                            var externals = externalsProperty.EnumerateArray();
                            foreach ( var external in externals )
                            {
                                var platform = external.GetProperty("platform").GetString();
                                var hash = external.GetProperty("hash").GetString();
                                ExternalMethods.Upsert(databaseContext, platform, hash, id, false);
                            }
                        }


                        Log.Verbose(
                            "[{Name}] got Token Symbol {Symbol}, Name {TokenName}, Fungible {Fungible}, Decimal {Decimal}, Database Id {Id}",
                            Name, tokenSymbol, tokenName, fungible, tokenDecimal, id);

                        updatedTokensCount++;
                    }
                }

                //technically not needed for tokens, but we still got the data here
                if ( response.RootElement.TryGetProperty("organizations", out var organizationsProperty) )
                {
                    var organizations = organizationsProperty.EnumerateArray();

                    foreach ( var organization in organizations )
                    {
                        var orgItem = OrganizationMethods.Upsert(databaseContext, organization.ToString(), false);

                        Log.Verbose(
                            "[{Name}] got Platform {Organization}",
                            Name, organization.ToString());

                        //getting addresses
                        var urlOrg = $"{Settings.Default.GetRest()}/api/getOrganization/{orgItem.NAME}";
                        var responseOrg = Client.APIRequest<JsonDocument>(urlOrg, out var stringResponseOrg, null, 10);
                        if ( responseOrg != null )
                        {
                            if ( responseOrg.RootElement.TryGetProperty("error", out var errorPropertyOrg) )
                                Log.Error("[{Name}] Cannot fetch Token info. Error: {Error}",
                                    Name, errorPropertyOrg.GetString());

                            if ( responseOrg.RootElement.TryGetProperty("members", out var membersProperty) )
                            {
                                var members = membersProperty.EnumerateArray();
                                Log.Verbose("[{Name}] got {Count} Addresses to process", Name, members.Count());
                                foreach ( var member in members )
                                    OrganizationAddressMethods.Upsert(databaseContext, orgItem, member.ToString(),
                                        chainId, false);
                            }
                        }

                        updatedOrganizationsCount++;
                    }
                }
            }

            if ( updatedTokensCount > 0 || updatedPlatformsCount > 0 || updatedOrganizationsCount > 0 )
                databaseContext.SaveChanges();
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information(
            "[{Name}] Token update took {UpdateTime} sec, {UpdatedTokensCount} tokens updated, {PlatformsUpdated} platforms updated, {OrganizationUpdated} Organizations updated",
            Name, Math.Round(updateTime.TotalSeconds, 3), updatedTokensCount, updatedPlatformsCount,
            updatedOrganizationsCount);
    }
}
