using Database.Main;
using GhostDevs.PluginEngine;
using Serilog;
using System;
using System.Linq;

namespace GhostDevs.Blockchain
{
    public partial class PhantasmaPlugin: Plugin, IBlockchainPlugin
    {
        // When we reach this number of processed infusions, we save received/processed results.
        private static readonly int maxInfusionUpdatesForOneSession = 1000;

        public void ProcessInfusionEvents()
        {
            DateTime startTime = DateTime.Now;

            int updatedInfusionsCount;

            using (var databaseContext = new MainDbContext())
            {
                var infusionEvents = databaseContext.Events.Where(x => x.ChainId == ChainId && x.Infusion == null && x.EventKind.NAME.ToUpper() == "INFUSION").Take(maxInfusionUpdatesForOneSession).ToList();

                updatedInfusionsCount = 0;
                for (var i = 0; i < infusionEvents.Count(); i++)
                {
                    var infusionEvent = infusionEvents[i];

                    var token = TokenMethods.Get(databaseContext, ChainId, infusionEvent.InfusedSymbol.SYMBOL);
                    if (token == null || token.FUNGIBLE == null)
                    {
                        Log.Warning($"[{Name}] Infusions: Token {infusionEvent.InfusedSymbol.SYMBOL} should be initialized in another thread first.");
                        continue;
                    }

                    var value = infusionEvent.INFUSED_VALUE;
                                
                    if(token.FUNGIBLE == true)
                    {
                        // It's a fungible token. We should apply decimals.
                        var decimals = (int)token.DECIMALS;
                        value = Phantasma.Numerics.UnitConversion.ToDecimal(Phantasma.Numerics.BigInteger.Parse(value), decimals).ToString();
                    }

                    InfusionMethods.Upsert(databaseContext, infusionEvent, infusionEvent.Nft, infusionEvent.InfusedSymbol.SYMBOL, value);
                    databaseContext.SaveChanges();

                    updatedInfusionsCount++;

                    if (updatedInfusionsCount == maxInfusionUpdatesForOneSession)
                        break;
                }
            }

            TimeSpan updateTime = DateTime.Now - startTime;
            Log.Information($"[{Name}] Infusions update took {Math.Round(updateTime.TotalSeconds, 3)} sec, {updatedInfusionsCount} infusions updated");
        }
    }
}
