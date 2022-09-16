using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Backend.PluginEngine;
using Database.Main;
using Phantasma.Core.Numerics;
using Serilog;

namespace Backend.Blockchain;

public partial class PhantasmaPlugin : Plugin, IBlockchainPlugin
{
    // When we reach this number of processed infusions, we save received/processed results.
    private const int MaxInfusionUpdatesForOneSession = 1000;


    private void ProcessInfusionEvents(int chainId)
    {
        var startTime = DateTime.Now;

        int updatedInfusionsCount;

        using ( var databaseContext = new MainDbContext() )
        {
            var infusionEvents = databaseContext.InfusionEvents
                .Where(x => x.Event.ChainId == chainId && x.Infusion == null && x.Event.Contract.NAME != null)
                .Take(MaxInfusionUpdatesForOneSession).ToList();

            updatedInfusionsCount = 0;
            foreach ( var infusionEvent in infusionEvents )
            {
                //TODO just for now, might not be needed later
                //should not be needed since we only work the infusion event table now
                if ( string.IsNullOrEmpty(infusionEvent.InfusedToken?.SYMBOL) ) continue;

                var token = TokenMethods.Get(databaseContext, chainId, infusionEvent.InfusedToken.SYMBOL);


                if ( token?.FUNGIBLE == null )
                {
                    Log.Warning("[{Name}] Infusions: Token {Symbol} should be initialized in another thread first",
                        Name, infusionEvent.InfusedToken.SYMBOL);
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

                InfusionMethods.Upsert(databaseContext, infusionEvent, infusionEvent.Event.Nft,
                    infusionEvent.InfusedToken.SYMBOL, value, token);
                databaseContext.SaveChanges();

                updatedInfusionsCount++;

                if ( updatedInfusionsCount == MaxInfusionUpdatesForOneSession ) break;
            }
        }

        var updateTime = DateTime.Now - startTime;
        Log.Information(
            "[{Name}] Infusions update took {UpdateTime} sec, {UpdatedInfusionsCount} infusions updated",
            Name, Math.Round(updateTime.TotalSeconds, 3), updatedInfusionsCount);
    }
}
