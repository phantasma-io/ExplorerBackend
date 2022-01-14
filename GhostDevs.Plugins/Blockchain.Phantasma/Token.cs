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
    private void InitNewTokens(int chainId)
    {
        var startTime = DateTime.Now;

        int updatedTokensCount;

        using ( MainDbContext databaseContext = new() )
        {
            var tokens = databaseContext.Tokens.Where(x => x.ChainId == chainId && x.FUNGIBLE == null).ToList();

            //maybe change to foreach
            updatedTokensCount = 0;
            foreach ( var tokenToUpdate in tokens )
            {
                var token = Client.APIRequest<JsonDocument>(
                    $"{Settings.Default.GetRest()}/api/getToken?symbol=" + tokenToUpdate.SYMBOL,
                    out var stringResponse,
                    null, 10);

                if ( token == null )
                {
                    Log.Error("[{Name}] Cannot fetch Phantasma {Symbol} token info. Unknown error", Name,
                        tokenToUpdate.SYMBOL);
                    continue;
                }

                if ( token.RootElement.TryGetProperty("error", out var errorProperty) )
                {
                    Log.Error(
                        "[{Name}] Cannot fetch Phantasma {Symbol} token info: Error: {Error}",
                        Name, tokenToUpdate.SYMBOL, errorProperty.GetString());
                    continue;
                }

                if ( token.RootElement.GetProperty("flags").GetString()!.Contains("Fungible") )
                {
                    tokenToUpdate.FUNGIBLE = true;
                    // It's a fungible token. We should apply decimals.
                    tokenToUpdate.DECIMALS = token.RootElement.GetProperty("decimals").GetInt32();
                }
                else
                    tokenToUpdate.FUNGIBLE = false;

                updatedTokensCount++;
            }

            if ( updatedTokensCount > 0 ) databaseContext.SaveChanges();
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information(
            "[{Name}] Token update took {UpdateTime} sec, {UpdatedTokensCount} tokens updated", Name,
            Math.Round(updateTime.TotalSeconds, 3), updatedTokensCount);
    }
}
