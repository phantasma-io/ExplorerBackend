using System;
using System.Globalization;
using System.Linq;
using Database.Main;
using GhostDevs.PluginEngine;
using Phantasma.Numerics;
using Serilog;

namespace GhostDevs.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    // When we reach this number of processed infusions, we save received/processed results.
    private static readonly int maxInfusionUpdatesForOneSession = 1000;


    private void ProcessInfusionEvents(int chainId)
    {
        var startTime = DateTime.Now;

        int updatedInfusionsCount;

        using ( var databaseContext = new MainDbContext() )
        {
            var infusionEvents = databaseContext.Events
                .Where(x => x.ChainId == chainId && x.Infusion == null && x.EventKind.NAME.ToUpper() == "INFUSION" &&
                            x.Contract.NAME != null)
                .Take(maxInfusionUpdatesForOneSession).ToList();

            updatedInfusionsCount = 0;
            foreach ( var infusionEvent in infusionEvents )
            {
                //TODO just for now, might not be needed later
                if ( string.IsNullOrEmpty(infusionEvent.InfusedSymbol?.SYMBOL) ) continue;

                var token = TokenMethods.Get(databaseContext, chainId, infusionEvent.InfusedSymbol.SYMBOL);

                if ( token?.FUNGIBLE == null )
                {
                    Log.Warning(
                        "[{Name}] Infusions: Token {Symbol} should be initialized in another thread first", Name,
                        infusionEvent.InfusedSymbol.SYMBOL);
                    continue;
                }

                var value = infusionEvent.INFUSED_VALUE;

                if ( token.FUNGIBLE )
                {
                    // It's a fungible token. We should apply decimals.
                    var decimals = token.DECIMALS;
                    value = UnitConversion.ToDecimal(BigInteger.Parse(value), decimals)
                        .ToString(CultureInfo.InvariantCulture);
                }

                InfusionMethods.Upsert(databaseContext, infusionEvent, infusionEvent.Nft,
                    infusionEvent.InfusedSymbol.SYMBOL, value);
                databaseContext.SaveChanges();

                updatedInfusionsCount++;

                if ( updatedInfusionsCount == maxInfusionUpdatesForOneSession ) break;
            }
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information(
            "[{Name}] Infusions update took {UpdateTime} sec, {UpdatedInfusionsCount} infusions updated",
            Name, Math.Round(updateTime.TotalSeconds, 3), updatedInfusionsCount);
    }
}
