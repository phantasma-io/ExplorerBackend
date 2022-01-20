using System;
using System.Text.Json;
using Database.Main;
using GhostDevs.Api;
using GhostDevs.PluginEngine;
using Serilog;

namespace GhostDevs.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    private void InitNewTokens(int chainId)
    {
        var startTime = DateTime.Now;

        int updatedTokensCount;

        using ( MainDbContext databaseContext = new() )
        {
            updatedTokensCount = 0;
            var url = $"{Settings.Default.GetRest()}/api/getNexus";

            var response = Client.APIRequest<JsonDocument>(url, out var stringResponse, null, 10);
            if ( response != null )
            {
                if ( response.RootElement.TryGetProperty("error", out var errorProperty) )
                    Log.Error("[{Name}] Cannot fetch Token info. Error: {Error}",
                        Name, errorProperty.GetString());

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
                        //external

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
                            address, owner, currentSupply, maxSupply, burnedSupply, scriptRaw);

                        Log.Verbose(
                            "[{Name}] got Token Symbol {Symbol}, Name {TokenName}, Fungible {Fungible}, Decimal {Decimal}, Database Id {Id}",
                            Name, tokenSymbol, tokenName, fungible, tokenDecimal, id);

                        updatedTokensCount++;
                    }
                }
            }

            if ( updatedTokensCount > 0 ) databaseContext.SaveChanges();
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information(
            "[{Name}] Token update took {UpdateTime} sec, {UpdatedTokensCount} tokens updated", Name,
            Math.Round(updateTime.TotalSeconds, 3), updatedTokensCount);
    }
}
