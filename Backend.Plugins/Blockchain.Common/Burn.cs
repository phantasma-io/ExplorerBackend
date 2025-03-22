using System;
using System.Linq;
using Backend.PluginEngine;
using Database.Main;
using Serilog;

namespace Backend.Blockchain;

public partial class BlockchainCommonPlugin : Plugin, IDBAccessPlugin
{
    private void MarkBurnedNfts()
    {
        var startTime = DateTime.Now;

        var markedEventCount = 0;
        var markedNftCount = 0;

        using ( var databaseContext = new MainDbContext() )
        {
            int[] chainIds = {ChainMethods.GetId(databaseContext, "main")};

            var burnEventId = databaseContext.EventKinds
                .Where(x => chainIds.Contains(x.ChainId) && x.NAME == "TokenBurn").Select(x => x.ID).First();

            while(true)
            {
                // TODO instead of checking for "KCAL"
                // we must add fungible flag to "Contracts" table.
                var burnedTokens = databaseContext.Events
                    .Where(x => x.EventKindId == burnEventId &&
                                x.BURNED != true &&
                                x.Contract.SYMBOL != "KCAL")
                    .Select(x => new {x.ContractId, x.TOKEN_ID})
                    .Distinct()
                    .Take(100)
                    .ToList();

                if(burnedTokens.Count() == 0)
                {
                    break;
                }

                foreach ( var burnedToken in burnedTokens )
                {
                    var tokenEvents = databaseContext.Events
                        .Where(x => x.ContractId == burnedToken.ContractId && x.TOKEN_ID == burnedToken.TOKEN_ID)
                        .ToList();

                    foreach ( var tokenEvent in tokenEvents )
                    {
                        tokenEvent.BURNED = true;
                        markedEventCount++;
                    }

                    var nft = databaseContext.Nfts
                        .FirstOrDefault(x =>
                            x.ContractId == burnedToken.ContractId && x.TOKEN_ID == burnedToken.TOKEN_ID);
                    if ( nft == null ) continue;
                    nft.BURNED = true;
                    markedNftCount++;
                }

                if ( markedEventCount > 0 || markedNftCount > 0 )
                {
                    try
                    {
                        databaseContext.SaveChanges();
                    }
                    catch ( Exception ex )
                    {
                        //TODO fix, cause it seems the exception text has changed
                        if ( ex.Message.Contains("Database operation expected to affect ") ||
                            ex.Message.Contains("database operation was expected to affect ") )
                        {
                            var attemptTime = DateTime.Now - startTime;
                            Log.Warning(
                                "{Name} plugin: Burned token events processing failed because some NFTs or events were deleted in another thread, attempt took {AttemptTime} sec",
                                Name, Math.Round(attemptTime.TotalSeconds, 3));
                            return;
                        }

                        throw;
                    }
                }
            }
        }

        var processTime = DateTime.Now - startTime;
        if(processTime.TotalSeconds > 1 || markedEventCount > 0 || markedNftCount > 0)
        {
            Log.Information(
                "{Name} plugin: Burned token events processing took {ProcessTime} sec, {MarkedEventCount} events marked, {MarkedNftCount} NFTs marked",
                Name, Math.Round(processTime.TotalSeconds, 3), markedEventCount, markedNftCount);
        }
    }
}
