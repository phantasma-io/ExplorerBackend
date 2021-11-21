using GhostDevs.Api;
using Database.Main;
using GhostDevs.PluginEngine;
using Serilog;
using System;
using System.Linq;
using System.Text.Json;

namespace GhostDevs.Blockchain
{
    public partial class PhantasmaPlugin: Plugin, IBlockchainPlugin
    {
        public void InitNewTokens()
        {
            DateTime startTime = DateTime.Now;

            int updatedTokensCount;

            using (var databaseContext = new MainDatabaseContext())
            {
                var tokens = databaseContext.Tokens.Where(x => x.ChainId == ChainId && x.FUNGIBLE == null).ToList();

                updatedTokensCount = 0;
                for (var i = 0; i < tokens.Count(); i++)
                {
                    var tokenToUpdate = tokens[i];

                    var token = Client.APIRequest<JsonDocument>($"{Settings.Default.GetRest()}/api/getToken?symbol=" + tokenToUpdate.SYMBOL, out var stringResponse, null, 10);

                    if (token == null)
                    {
                        Log.Error($"[{Name}] Cannot fetch Phantasma {tokenToUpdate.SYMBOL} token info. Unknown error");
                        continue;
                    }

                    if (token.RootElement.TryGetProperty("error", out var errorProperty))
                    {
                        Log.Error($"[{Name}] Cannot fetch Phantasma {tokenToUpdate.SYMBOL} token info: Error: {errorProperty.GetString()}");
                        continue;
                    }

                    if (token.RootElement.GetProperty("flags").GetString().Contains("Fungible"))
                    {
                        tokenToUpdate.FUNGIBLE = true;
                        // It's a fungible token. We should apply decimals.
                        tokenToUpdate.DECIMALS = token.RootElement.GetProperty("decimals").GetInt32();
                    }
                    else
                    {
                        tokenToUpdate.FUNGIBLE = false;
                    }

                    updatedTokensCount++;
                }

                if(updatedTokensCount > 0)
                    databaseContext.SaveChanges();
            }

            TimeSpan updateTime = DateTime.Now - startTime;
            Log.Information($"[{Name}] Token update took {Math.Round(updateTime.TotalSeconds, 3)} sec, {updatedTokensCount} tokens updated");
        }
    }
}
